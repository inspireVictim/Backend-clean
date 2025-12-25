using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides; // Добавлено для корректного определения IP
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using YessBackend.Api.Middleware;
using YessBackend.Application.Config;
using YessBackend.Application.Extensions;
using YessBackend.Application.Interfaces.Payments;
using YessBackend.Application.Services;
using YessBackend.Infrastructure.Data;
using YessBackend.Infrastructure.Extensions;
using YessBackend.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

var jwtSecret = configuration["Jwt:SecretKey"] ?? throw new Exception("JWT SecretKey missing");
var jwtIssuer = configuration["Jwt:Issuer"] ?? "YessBackend";
var jwtAudience = configuration["Jwt:Audience"] ?? "YessUsers";

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

// Конфигурация сервисов
builder.Services.Configure<FinikPaymentConfig>(configuration.GetSection("FinikPayment"));
builder.Services.AddScoped<IFinikSignatureService, FinikSignatureService>();
builder.Services.AddHttpClient<IFinikPaymentService, FinikPaymentService>();
builder.Services.AddScoped<IFinikPaymentService, FinikPaymentService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddScoped<IOptimaPaymentService, OptimaPaymentService>();

builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowCors", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddHttpClient();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(configuration);
builder.Services.AddYessBackendServices();

var app = builder.Build();

// --- ВАЖНОЕ ИСПРАВЛЕНИЕ ДЛЯ ОПРЕДЕЛЕНИЯ IP ---
// Позволяет приложению читать заголовок X-Forwarded-For, который передает Nginx/Proxy
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
// ---------------------------------------------

app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "YESS API v1");
    c.RoutePrefix = "docs";
});

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseCors("AllowCors");

var bannersPath = Path.Combine(app.Environment.ContentRootPath, "Storage", "Banners");
if (!Directory.Exists(bannersPath)) Directory.CreateDirectory(bannersPath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(bannersPath),
    RequestPath = "/content/banners"
});

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();