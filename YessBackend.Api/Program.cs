using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Models; // Для OpenApiInfo
using System.Text;
using System.Text.Json;
using YessBackend.Api.Middleware;
using YessBackend.Application.Config;
using YessBackend.Application.Extensions;
using YessBackend.Application.Interfaces.Payments;
using YessBackend.Application.Services;
using YessBackend.Infrastructure.Data;
using YessBackend.Infrastructure.Extensions;
using YessBackend.Infrastructure.Services;

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

// Регистрация сервисов платежей
builder.Services.AddHttpClient<IFinikPaymentService, FinikPaymentService>();
builder.Services.AddScoped<IFinikPaymentService, FinikPaymentService>();
builder.Services.AddScoped<IWebhookService, WebhookService>();
builder.Services.AddScoped<IOptimaPaymentService, OptimaPaymentService>();

// ====== Controllers ======
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        // Устанавливаем camelCase для фронтенда
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// ====== CORS ======
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowCors", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
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

// ====== ИСПРАВЛЕННЫЙ SWAGGER GEN ======
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "YESS API", Version = "v1" });
    // Фикс для корректной работы UploadBanner (обработка файлов)
    c.MapType<IFormFile>(() => new OpenApiSchema { Type = "string", Format = "binary" });
});

// ====== EF Core ======
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"));
    options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

builder.Services.AddHttpClient();

// ВОЗВРАЩАЕМ ВАШИ ОРИГИНАЛЬНЫЕ МЕТОДЫ РАСШИРЕНИЯ
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

// ====== РАЗДАЧА БАННЕРОВ (STATIC FILES) ======
var bannersPath = Path.Combine(app.Environment.ContentRootPath, "Storage", "Banners");
if (!Directory.Exists(bannersPath))
{
    Directory.CreateDirectory(bannersPath);
}

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