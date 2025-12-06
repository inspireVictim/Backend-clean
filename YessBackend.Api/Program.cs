using YessBackend.Application.Config;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using YessBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using YessBackend.Application.Extensions;
using YessBackend.Infrastructure.Extensions;
using YessBackend.Application.Services;
using YessBackend.Infrastructure.Services;
using YessBackend.Api.Middleware;
using YessBackend.Application.Interfaces.Payments;

var builder = WebApplication.CreateBuilder(args);

// =======================
//      Server HTTP 5000
// =======================
builder.WebHost.UseKestrel(options =>
{
    options.ListenAnyIP(5000);
});

var configuration = builder.Configuration;

// =======================
//     Finik Payment
// =======================
builder.Services.Configure<FinikPaymentConfig>(
    configuration.GetSection("FinikPayment"));

// Используем FinikPaymentService — корректное имя класса
builder.Services.AddHttpClient<IFinikPaymentService, FinikPaymentService>();

// =======================
//       Controllers
// =======================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
    });

// =======================
//          CORS
// =======================
var corsOrigins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowCors", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();

        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
                origin.Contains("localhost") || origin.Contains("127.0.0.1"));
        }
    });
});

// =======================
//          JWT
// =======================
var jwtSettings = configuration.GetSection("Jwt");
var secretKey = jwtSettings["SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey не настроен");

var key = Encoding.UTF8.GetBytes(secretKey);

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
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidateAudience = true,
        ValidAudience = jwtSettings["Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// =======================
//        Swagger
// =======================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// =======================
//    PostgreSQL EF Core
// =======================
var connectionString = configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' не найден");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString);

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// =======================
//         Redis
// =======================
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration["Redis:ConnectionString"] ?? "localhost:6379";
    options.InstanceName = "YessBackend:";
});

// =======================
//   Base services
// =======================
builder.Services.AddHttpClient();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(configuration);
builder.Services.AddYessBackendServices();


// Background Workers
builder.Services.AddHostedService<YessBackend.Infrastructure.Services.ReconciliationBackgroundService>();

var app = builder.Build();

// =======================
//   AUTO APPLY MIGRATIONS
// =======================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}


// =======================
//        Swagger UI
// =======================
if (app.Environment.IsDevelopment() || configuration.GetValue<bool>("EnableSwagger", false))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "YESS API v1");
        c.RoutePrefix = "docs";
    });
}

// =======================
//  Middleware pipeline
// =======================
app.UseGlobalExceptionHandler();
app.UseRateLimiting(configuration);
app.UseCors("AllowCors");
app.UseAuthentication();
app.UseAuthorization();

// =======================
//        Endpoints
// =======================
app.MapGet("/", () => new
{
    status = "ok",
    service = "yess-backend",
    api = "/api/v1",
    docs = "/docs"
});

app.MapGet("/health", () => new
{
    status = "healthy",
    service = "yess-backend",
    version = "1.0.0",
    timestamp = DateTime.UtcNow
});

app.MapControllers();
app.Run();
