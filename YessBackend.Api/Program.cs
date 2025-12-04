using System.Net;
using System.Text;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
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
using YessBackend.Domain.Entities;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Настройка конфигурации для поддержки переменных окружения
// Переменные окружения имеют формат: ASPNETCORE_KESTREL__CERTIFICATE__PATH (двойное подчёркивание)
builder.Configuration.AddEnvironmentVariables(prefix: "ASPNETCORE_");

// Создаём временный logger для ConfigureKestrel (до полной инициализации)
var tempLoggerFactory = LoggerFactory.Create(logging => 
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});
var kestrelLogger = tempLoggerFactory.CreateLogger("Kestrel");

// Настройка Kestrel
// HTTP на 5000 всегда включён для обратного прокси (nginx)
// HTTPS на 5001 всегда включён (dev-сертификат в Development, production сертификат в Production)
builder.WebHost.ConfigureKestrel(options =>
{
    // Настройка многопоточности для поддержки 10-15 одновременных соединений (требование QIWI OSMP v1.4)
    var maxConcurrentConnections = builder.Configuration.GetValue<int>("Kestrel:Limits:MaxConcurrentConnections", 15);
    options.Limits.MaxConcurrentConnections = maxConcurrentConnections;
    options.Limits.MaxConcurrentUpgradedConnections = maxConcurrentConnections;
    
    // Таймаут для запросов: 60 секунд (требование QIWI OSMP v1.4)
    options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(60);
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(60);
    
    // HTTP endpoint всегда включён для обратного прокси (nginx)
    options.ListenAnyIP(5000);
    kestrelLogger.LogInformation("HTTP настроен на порту 5000 для обратного прокси");
    
    // HTTPS настройка в зависимости от окружения
    if (builder.Environment.IsDevelopment())
    {
        // Development: Используем встроенный dev-сертификат (UseHttpsDeveloperCertificate)
        options.ListenAnyIP(5001, listenOptions =>
        {
            listenOptions.UseHttps(); // Автоматически использует dev-сертификат ASP.NET Core
        });
        kestrelLogger.LogInformation("HTTPS настроен для Development на порту 5001 с dev-сертификатом");
    }
    else
    {
        // Production: Загружаем сертификат из переменных окружения
        // Переменные окружения читаются через Configuration (двойное подчёркивание преобразуется в двоеточие)
        // Используем правильную секцию: Kestrel:Certificates:Default:Path
        var certPath = builder.Configuration["Kestrel:Certificates:Default:Path"];
        var certPassword = builder.Configuration["Kestrel:Certificates:Default:Password"];
        
        // Проверка пути к сертификату
        if (string.IsNullOrWhiteSpace(certPath))
        {
            kestrelLogger.LogWarning(
                "HTTPS не настроен: переменная окружения ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PATH не задана. " +
                "Приложение будет работать только по HTTP на порту 5000");
        }
        else if (!File.Exists(certPath))
        {
            kestrelLogger.LogWarning(
                "HTTPS не настроен: файл сертификата не найден по пути '{CertPath}'. " +
                "Приложение будет работать только по HTTP на порту 5000",
                certPath);
        }
        else
        {
            // Попытка настроить HTTPS с сертификатом
            try
            {
                options.ListenAnyIP(5001, listenOptions =>
                {
                    if (string.IsNullOrWhiteSpace(certPassword))
                    {
                        // Сертификат без пароля
                        listenOptions.UseHttps(certPath);
                    }
                    else
                    {
                        // Сертификат с паролем
                        listenOptions.UseHttps(certPath, certPassword);
                    }
                });
                
                kestrelLogger.LogInformation(
                    "HTTPS настроен для Production на порту 5001 с сертификатом '{CertPath}'",
                    certPath);
            }
            catch (CryptographicException ex)
            {
                kestrelLogger.LogWarning(ex,
                    "Не удалось загрузить HTTPS сертификат '{CertPath}': {Message}. " +
                    "Проверьте правильность пароля и формат файла. HTTPS не будет настроен. " +
                    "Приложение будет работать только по HTTP на порту 5000",
                    certPath, ex.Message);
            }
            catch (Exception ex)
            {
                kestrelLogger.LogWarning(ex,
                    "Неожиданная ошибка при загрузке сертификата '{CertPath}': {Message}. HTTPS не будет настроен. " +
                    "Приложение будет работать только по HTTP на порту 5000",
                    certPath, ex.Message);
            }
        }
    }
});

// Настройка конфигурации
var configuration = builder.Configuration;
var jwtSettings = configuration.GetSection("Jwt");

// Добавляем Controllers (вместо Minimal APIs для совместимости с FastAPI стилем)
// Настраиваем поддержку form-urlencoded для OAuth2 совместимости
builder.Services.AddControllers(options =>
{
    // Разрешаем обработку form-urlencoded
    options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
})
.AddJsonOptions(options =>
{
    // Настройки JSON сериализации
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.WriteIndented = builder.Environment.IsDevelopment();
})
.ConfigureApiBehaviorOptions(options =>
{
    // Отключаем автоматическую валидацию модели для form-urlencoded endpoints
    // (валидация будет выполняться вручную в контроллерах)
    options.SuppressModelStateInvalidFilter = false;
});

// CORS настройка
var corsOrigins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowCors", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
        
        // В development режиме разрешаем любые localhost порты
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin => 
                origin.Contains("localhost") || origin.Contains("127.0.0.1"));
        }
    });
});

// JWT Authentication
var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey не настроен");
var key = Encoding.UTF8.GetBytes(secretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
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

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    // Исправляем проблемы с именами схем (избегаем конфликтов имен типов)
    options.CustomSchemaIds(type => type.FullName?.Replace("+", ".") ?? type.Name);
    
    // Игнорируем устаревшие действия и свойства
    options.IgnoreObsoleteActions();
    options.IgnoreObsoleteProperties();
    
    // Обрабатываем конфликтующие действия (берем первое)
    options.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());
    
    // Временно исключаем endpoints с IFormFile из Swagger документации
    // Для Swashbuckle 6+ требуется специальный OperationFilter с Microsoft.OpenApi.Models
    // Endpoints будут работать нормально, но не будут отображаться в Swagger UI
    options.DocInclusionPredicate((docName, apiDesc) =>
    {
        // Исключаем UploadController endpoints до настройки правильного фильтра
        if (apiDesc.RelativePath?.Contains("/upload") == true)
        {
            return false;
        }
        return true;
    });
    
    // Включаем XML комментарии (если есть)
    try
    {
        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }
    catch
    {
        // Игнорируем ошибки при поиске XML файлов
    }
});

// EF Core - PostgreSQL
var connectionString = configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' не найден");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(30); // Таймаут запроса 30 секунд
        npgsqlOptions.MigrationsAssembly("YessBackend.Infrastructure");
    });
    
    // Логирование SQL только в Development
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

// Redis
var redisConnectionString = configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "YessBackend:";
});

// HttpClient для внешних API (OSRM, GraphHopper и т.д.)
builder.Services.AddHttpClient();

// Регистрация сервисов
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(configuration);

// Регистрация сервисов приложения
builder.Services.AddScoped<IAuthService, YessBackend.Infrastructure.Services.AuthService>();
builder.Services.AddScoped<IWalletService, YessBackend.Infrastructure.Services.WalletService>();
builder.Services.AddScoped<IPartnerService, YessBackend.Infrastructure.Services.PartnerService>();
builder.Services.AddScoped<IOrderService, YessBackend.Infrastructure.Services.OrderService>();
builder.Services.AddScoped<IHealthService, YessBackend.Infrastructure.Services.HealthService>();
builder.Services.AddScoped<IRouteService, YessBackend.Infrastructure.Services.RouteService>();
builder.Services.AddScoped<ILocationService, YessBackend.Infrastructure.Services.LocationService>();
builder.Services.AddScoped<IStorageService, YessBackend.Infrastructure.Services.StorageService>();
builder.Services.AddScoped<IQRService, YessBackend.Infrastructure.Services.QRService>();
builder.Services.AddScoped<IStoryService, YessBackend.Infrastructure.Services.StoryService>();
builder.Services.AddScoped<IPartnerProductService, YessBackend.Infrastructure.Services.PartnerProductService>();
builder.Services.AddScoped<IOrderPaymentService, YessBackend.Infrastructure.Services.OrderPaymentService>();
builder.Services.AddScoped<IWebhookService, YessBackend.Infrastructure.Services.WebhookService>();
builder.Services.AddScoped<IPaymentProviderService, YessBackend.Infrastructure.Services.PaymentProviderService>();
builder.Services.AddScoped<INotificationService, YessBackend.Infrastructure.Services.NotificationService>();
builder.Services.AddScoped<IAchievementService, YessBackend.Infrastructure.Services.AchievementService>();
builder.Services.AddScoped<IPromotionService, YessBackend.Infrastructure.Services.PromotionService>();
builder.Services.AddScoped<IBankService, YessBackend.Infrastructure.Services.BankService>();
builder.Services.AddScoped<YessBackend.Application.Services.IOptimaPaymentService, YessBackend.Infrastructure.Services.OptimaPaymentService>();
builder.Services.AddScoped<YessBackend.Application.Services.IEmailService, YessBackend.Infrastructure.Services.EmailService>();
builder.Services.AddScoped<YessBackend.Application.Services.IReconciliationService, YessBackend.Infrastructure.Services.ReconciliationService>();

// Background Service для ежедневной сверки
builder.Services.AddHostedService<YessBackend.Infrastructure.Services.ReconciliationBackgroundService>();

var app = builder.Build();

// Configure the HTTP request pipeline
// Swagger ДОЛЖЕН быть ПЕРЕД глобальным обработчиком исключений,
// чтобы видеть реальные ошибки при генерации документации

// Swagger доступен в Development или если явно включен через переменную окружения
var enableSwagger = app.Environment.IsDevelopment() || 
                    configuration.GetValue<bool>("EnableSwagger", false);

if (enableSwagger)
{
    // Swagger middleware ПЕРЕД обработчиком исключений
    // Добавляем обработку ошибок для Swagger
    app.UseSwagger(c =>
    {
        c.RouteTemplate = "swagger/{documentName}/swagger.json";
    });
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "YESS API v1");
        c.RoutePrefix = "docs"; // /docs для совместимости с FastAPI
        c.DisplayRequestDuration();
    });
}

// HTTPS Redirection всегда включён в Development (dev-сертификат всегда доступен)
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
else
{
    // Production: HTTPS redirect и HSTS только если HTTPS настроен
    // Используем правильную секцию: Kestrel:Certificates:Default:Path
    var certPath = configuration["Kestrel:Certificates:Default:Path"];
    var httpsConfigured = !string.IsNullOrWhiteSpace(certPath) && File.Exists(certPath);
    
    if (httpsConfigured)
    {
        app.UseHttpsRedirection();
        app.UseHsts();
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("HTTPS Redirection и HSTS включены для Production");
    }
    else
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning("HTTPS не настроен - HTTPS Redirection и HSTS отключены");
    }
}

// Глобальный обработчик исключений (после Swagger, чтобы не перехватывать ошибки генерации документации)
app.UseGlobalExceptionHandler();

// Rate Limiting
app.UseRateLimiting(configuration);

// CORS должен быть перед UseAuthentication
app.UseCors("AllowCors");

app.UseAuthentication();
app.UseAuthorization();

// Root endpoint
app.MapGet("/", () => new
{
    status = "ok",
    service = "yess-backend",
    api = "/api/v1",
    docs = "/docs"
});

// Health check endpoint (базовый, без проверки БД)
// Основной health check находится в HealthController: /api/v1/health
app.MapGet("/health", () => new
{
    status = "healthy",
    service = "yess-backend",
    version = "1.0.0",
    timestamp = DateTime.UtcNow
});

// /api/v1/health определен в HealthController - удаляем дубликат Minimal API

// Health check для базы данных
app.MapGet("/health/db", async (ApplicationDbContext db) =>
{
    try
    {
        // Проверяем подключение к БД
        var canConnect = await db.Database.CanConnectAsync();
        if (canConnect)
        {
            return Results.Ok(new
            {
                status = "healthy",
                database = "connected",
                timestamp = DateTime.UtcNow
            });
        }
        else
        {
            return Results.StatusCode(503);
        }
    }
    catch
    {
        return Results.StatusCode(503);
    }
});

// Автоматическое применение миграций при старте приложения
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    
    try
    {
        // Проверяем, включено ли автоматическое применение миграций
        var autoMigrate = configuration.GetValue<bool>("Database:AutoMigrate", true);
        
        if (!autoMigrate)
        {
            logger.LogInformation("Автоматическое применение миграций отключено в конфигурации.");
        }
        else
        {
            var dbContext = services.GetRequiredService<ApplicationDbContext>();
            var migrationTimeout = configuration.GetValue<int>("Database:MigrationTimeoutSeconds", 60);
            
            // Проверяем подключение к базе данных
            logger.LogInformation("Проверка подключения к базе данных...");
            var canConnect = await dbContext.Database.CanConnectAsync();
            
            if (!canConnect)
            {
                logger.LogWarning("Не удалось подключиться к базе данных. Миграции не будут применены.");
                if (app.Environment.IsDevelopment())
                {
                    throw new InvalidOperationException("Не удалось подключиться к базе данных. Проверьте строку подключения.");
                }
            }
            else
            {
                logger.LogInformation("Подключение к базе данных установлено.");
                
                // Получаем список ожидающих миграций
                var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
                var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
                
                logger.LogInformation("Текущие миграции в БД: {Count}", appliedMigrations.Count());
                
                if (pendingMigrations.Any())
                {
                    logger.LogInformation("Найдено {Count} ожидающих миграций:", pendingMigrations.Count());
                    foreach (var migration in pendingMigrations)
                    {
                        logger.LogInformation("  → {Migration}", migration);
                    }
                    
                    logger.LogInformation("Применение миграций (таймаут: {Timeout} секунд)...", migrationTimeout);
                    
                    // Применяем все ожидающие миграции с таймаутом
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(migrationTimeout)))
                    {
                        await dbContext.Database.MigrateAsync(cts.Token);
                    }
                    
                    logger.LogInformation("✅ Все миграции успешно применены.");
                    
                    // Проверяем, что миграции действительно применены
                    var stillPending = await dbContext.Database.GetPendingMigrationsAsync();
                    if (stillPending.Any())
                    {
                        logger.LogWarning("⚠️ Внимание: после применения миграций остались ожидающие: {Migrations}", 
                            string.Join(", ", stillPending));
                    }
                    else
                    {
                        logger.LogInformation("✅ База данных полностью актуальна.");
                    }
                }
                else
                {
                    logger.LogInformation("✅ База данных актуальна, ожидающих миграций нет.");
                }
            }
        }

        // Seed тестового пользователя для мобильного клиента
        try
        {
            var dbContext = services.GetRequiredService<ApplicationDbContext>();
            var authService = services.GetRequiredService<IAuthService>();
            const string testPhone = "+996504876087";
            const string testPassword = "123456";

            var existingUser = await authService.GetUserByPhoneAsync(testPhone);
            if (existingUser == null)
            {
                var user = new User
                {
                    Phone = testPhone,
                    Email = "testuser@example.com",
                    FirstName = "Test",
                    LastName = "User",
                    PasswordHash = authService.HashPassword(testPassword),
                    PhoneVerified = true,
                    EmailVerified = false,
                    IsActive = true,
                    IsBlocked = false,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();

                var wallet = new Wallet
                {
                    UserId = user.Id,
                    Balance = 0.0m,
                    YescoinBalance = 0.0m,
                    TotalEarned = 0.0m,
                    TotalSpent = 0.0m,
                    LastUpdated = DateTime.UtcNow
                };

                dbContext.Wallets.Add(wallet);
                await dbContext.SaveChangesAsync();

                logger.LogInformation("Создан тестовый пользователь {Phone} с паролем {Password}", testPhone, testPassword);
            }
            else
            {
                logger.LogInformation("Тестовый пользователь {Phone} уже существует (Id={Id})", existingUser.Phone, existingUser.Id);
            }
        }
        catch (Exception seedEx)
        {
            logger.LogError(seedEx, "Ошибка при создании тестового пользователя.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Ошибка при применении миграций базы данных.");
        // В Development режиме выбрасываем исключение, чтобы увидеть проблему
        if (app.Environment.IsDevelopment())
        {
            throw;
        }
        // В Production продолжаем работу, но логируем ошибку
    }
}

// Map Controllers
app.MapControllers();

app.Run();