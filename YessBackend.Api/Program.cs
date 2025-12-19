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
using Microsoft.EntityFrameworkCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Конфигурация
var configuration = builder.Configuration;

// ПРИОРИТЕТ ДЛЯ JWT ИЗ DOCKER (Фикс ошибки 401)
var jwtSecret = configuration["Jwt:SecretKey"] ?? throw new Exception("JWT SecretKey missing in config");
var jwtIssuer = configuration["Jwt:Issuer"] ?? "YessBackend";
var jwtAudience = configuration["Jwt:Audience"] ?? "YessUsers";

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

// ====== FINIK Payment ======
builder.Services.Configure<FinikPaymentConfig>(configuration.GetSection("FinikPayment"));
builder.Services.AddScoped<IFinikSignatureService, FinikSignatureService>();
builder.Services.AddHttpClient<IFinikPaymentService, FinikPaymentService>();

// ====== Controllers ======
builder.Services.AddControllers()
    .AddJsonOptions(options => {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
    });

// ====== CORS ======
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowCors", policy =>
    {
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader();
    });
});

// ====== JWT AUTHENTICATION (Синхронизировано с Docker) ======
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

app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "YESS API v1");
    c.RoutePrefix = "docs";
});

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseCors("AllowCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();
