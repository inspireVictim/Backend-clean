using YessBackend.Application.Config;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Logging;
using YessBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using YessBackend.Application.Extensions;
using YessBackend.Infrastructure.Extensions;
using YessBackend.Application.Services;
using YessBackend.Infrastructure.Services;
using YessBackend.Api.Middleware;
using YessBackend.Application.Interfaces.Payments;

var builder = WebApplication.CreateBuilder(args);

// =============================================
//   KESTREL — ТОЛЬКО HTTP (Production via nginx)
// =============================================

builder.WebHost.ConfigureKestrel(options =>
{
    // HTTP порт — рабочий для nginx reverse-proxy
    options.ListenAnyIP(5000);

    // HTTPS оставляем только для Development
    if (builder.Environment.IsDevelopment())
    {
        options.ListenAnyIP(5001, listen =>
        {
            listen.UseHttps();
        });
    }
});

// =============================================
//       CONFIGURATION
// =============================================
var configuration = builder.Configuration;

// ====== FINIK Payment ======
builder.Services.Configure<FinikPaymentConfig>(configuration.GetSection("FinikPayment"));
builder.Services.AddScoped<IFinikSignatureService, FinikSignatureService>();

builder.Services.AddHttpClient<IFinikPaymentService, FinikPaymentService>()
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AllowAutoRedirect = false
    });

// ====== Controllers ======
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.DictionaryKeyPolicy = null;
    });

// ====== CORS ======
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

// ====== JWT ======
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
    options.RequireHttpsMetadata = false; // nginx → backend = HTTP
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

// ====== Swagger ======
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ====== EF Core ======
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

// ====== Redis ======
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = configuration["Redis:ConnectionString"] ?? "localhost:6379";
    options.InstanceName = "YessBackend:";
});

// ====== App Services ======
builder.Services.AddHttpClient();
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(configuration);
builder.Services.AddYessBackendServices();
builder.Services.AddHostedService<ReconciliationBackgroundService>();

var app = builder.Build();

// ====== Apply Migrations ======
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

// =============================================
//  🚫 HTTPS REDIRECTION — ОТКЛЮЧЕНО В PROD
//  nginx уже занимается HTTPS
// =============================================
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// ====== Swagger UI ======
if (app.Environment.IsDevelopment() || configuration.GetValue<bool>("EnableSwagger", false))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "YESS API v1");
        c.RoutePrefix = "docs";
    });
}

// ====== Middleware ======
app.UseGlobalExceptionHandler();
app.UseRateLimiting(configuration);
app.UseCors("AllowCors");
app.UseAuthentication();
app.UseAuthorization();

// ====== Endpoints ======
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
