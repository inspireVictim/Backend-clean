using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using YessBackend.Application.Config;
using YessBackend.Infrastructure.Data;
using YessBackend.Application.Extensions;
using YessBackend.Infrastructure.Extensions;
using YessBackend.Application.Services;
using YessBackend.Infrastructure.Services;
using YessBackend.Api.Middleware;
using YessBackend.Application.Interfaces.Payments;

var builder = WebApplication.CreateBuilder(args);

// Конфигурация
var configuration = builder.Configuration;

// ПРИОРИТЕТ ДЛЯ JWT ИЗ DOCKER
var jwtSecret = configuration["Jwt:SecretKey"] ?? throw new Exception("JWT SecretKey missing in config");
var jwtIssuer = configuration["Jwt:Issuer"] ?? "YessBackend";
var jwtAudience = configuration["Jwt:Audience"] ?? "YessUsers";

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

// ====== FINIK Payment & SERVICES ======
builder.Services.Configure<FinikPaymentConfig>(configuration.GetSection("FinikPayment"));
builder.Services.AddScoped<IFinikSignatureService, FinikSignatureService>();

// Регистрируем наш сервис пополнения (убеждаемся, что он доступен)
builder.Services.AddHttpClient<IFinikPaymentService, FinikPaymentService>();
builder.Services.AddScoped<IFinikPaymentService, FinikPaymentService>();

builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddScoped<IOptimaPaymentService, OptimaPaymentService>();

// ====== Controllers ======
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        // null сохраняет имена как в C# (PascalCase), 
        // но для фронтенда лучше использовать JsonNamingPolicy.CamelCase
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// ====== CORS (Разрешаем фронтенду доступ) ======
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowCors", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ====== JWT AUTHENTICATION ======
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
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

// ====== EF Core ======
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

// ====== SWAGGER ======
app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "YESS API v1");
    c.RoutePrefix = "docs";
});

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseCors("AllowCors");

// ====== РАЗДАЧА БАННЕРОВ ======
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