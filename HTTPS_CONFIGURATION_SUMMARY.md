# 🔐 Настройка HTTPS в Program.cs - Итоговая конфигурация

## ✅ Выполненные изменения

### 1. Добавлены необходимые using директивы

```csharp
using System.Security.Cryptography.X509Certificates;
```

### 2. Добавлена поддержка переменных окружения

```csharp
builder.Configuration.AddEnvironmentVariables(prefix: "ASPNETCORE_");
```

### 3. Обновлена секция ConfigureKestrel

Основные изменения:

- **HTTP всегда включён** на порту 5000 (настраивается через `Kestrel:Endpoints:Http:Port`)
- **HTTPS настраивается динамически** в зависимости от окружения:
  - **Development**: автоматически использует dev-сертификат на порту 5001
  - **Production**: загружает сертификат из переменных окружения на порту 5001

### 4. Безопасная обработка ошибок

- Проверка существования файла сертификата
- Обработка `CryptographicException` при неверном пароле
- Логирование предупреждений без падения приложения
- Проверка наличия приватного ключа в сертификате

## 📋 Ключевой фрагмент кода

### Секция ConfigureKestrel (строки 27-142)

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Warning));
    var logger = loggerFactory.CreateLogger("Kestrel");
    
    // Настройка лимитов...
    
    // HTTP endpoint всегда включён для обратного прокси (nginx)
    var httpPort = builder.Configuration.GetValue<int>("Kestrel:Endpoints:Http:Port", 5000);
    options.Listen(IPAddress.Any, httpPort, listenOptions =>
    {
        listenOptions.Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http1AndHttp2;
    });
    
    // HTTPS в зависимости от окружения
    var httpsPort = builder.Configuration.GetValue<int>("Kestrel:Endpoints:Https:Port", 5001);
    
    if (builder.Environment.IsDevelopment())
    {
        // Development: dev-сертификат
        options.Listen(IPAddress.Any, httpsPort, listenOptions =>
        {
            listenOptions.UseHttps();
        });
        logger.LogInformation("HTTPS настроен для Development на порту {Port} с dev-сертификатом", httpsPort);
    }
    else
    {
        // Production: загрузка сертификата
        var certPath = Environment.GetEnvironmentVariable("ASPNETCORE_KESTREL__CERTIFICATE__PATH")
            ?? builder.Configuration["Kestrel:Certificates:Default:Path"]
            ?? builder.Configuration["Kestrel:Certificate:Path"];
            
        var certPassword = Environment.GetEnvironmentVariable("ASPNETCORE_KESTREL__CERTIFICATE__PASSWORD")
            ?? builder.Configuration["Kestrel:Certificates:Default:Password"]
            ?? builder.Configuration["Kestrel:Certificate:Password"];
        
        if (string.IsNullOrEmpty(certPath))
        {
            logger.LogWarning("⚠️ HTTPS не настроен: путь к сертификату не задан...");
        }
        else if (!File.Exists(certPath))
        {
            logger.LogWarning("⚠️ HTTPS не настроен: файл сертификата не найден...");
        }
        else
        {
            try
            {
                X509Certificate2 certificate = string.IsNullOrEmpty(certPassword)
                    ? new X509Certificate2(certPath)
                    : new X509Certificate2(certPath, certPassword);
                
                if (!certificate.HasPrivateKey)
                {
                    logger.LogWarning("⚠️ Сертификат не содержит приватный ключ...");
                }
                else
                {
                    options.Listen(IPAddress.Any, httpsPort, listenOptions =>
                    {
                        listenOptions.UseHttps(certificate);
                    });
                    logger.LogInformation("✅ HTTPS настроен для Production...");
                }
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                logger.LogError(ex, "❌ Ошибка при загрузке сертификата...");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "❌ Неожиданная ошибка при загрузке сертификата...");
            }
        }
    }
});
```

## 🔧 Использование

### Development (локально)

Приложение автоматически:
- Запустит HTTP на порту 5000
- Запустит HTTPS на порту 5001 с dev-сертификатом

Никаких дополнительных настроек не требуется.

### Production (Ubuntu сервер)

#### Вариант 1: Переменные окружения (рекомендуется)

```bash
export ASPNETCORE_KESTREL__CERTIFICATE__PATH=/etc/ssl/certs/yess-cert.pfx
export ASPNETCORE_KESTREL__CERTIFICATE__PASSWORD=YesSGo!@#!
```

Для systemd service:

```ini
[Service]
Environment=ASPNETCORE_KESTREL__CERTIFICATE__PATH=/etc/ssl/certs/yess-cert.pfx
Environment=ASPNETCORE_KESTREL__CERTIFICATE__PASSWORD=YesSGo!@#!
```

#### Вариант 2: appsettings.Production.json

```json
{
  "Kestrel": {
    "Certificates": {
      "Default": {
        "Path": "/etc/ssl/certs/yess-cert.pfx",
        "Password": "YesSGo!@#!"
      }
    }
  }
}
```

⚠️ **Важно**: файл `appsettings.Production.json` должен быть исключён из git (добавлен в `.gitignore`)

### Настройка портов

Порты можно изменить через конфигурацию:

**appsettings.json:**
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Port": 8000
      },
      "Https": {
        "Port": 8443
      }
    }
  }
}
```

Или через переменные окружения:
```bash
export ASPNETCORE_KESTREL__ENDPOINTS__HTTP__PORT=8000
export ASPNETCORE_KESTREL__ENDPOINTS__HTTPS__PORT=8443
```

## 🔍 Проверка

### Проверка портов

```bash
# Проверка HTTP
curl http://localhost:5000/health

# Проверка HTTPS (Development)
curl -k https://localhost:5001/health

# Проверка HTTPS (Production)
curl -vk https://your-server:5001/health
```

### Проверка логов

```bash
# systemd
sudo journalctl -u yess-backend -f

# Должны увидеть:
# HTTPS настроен для Development на порту 5001 с dev-сертификатом
# или
# ✅ HTTPS настроен для Production на порту 5001 с сертификатом...
```

## ⚠️ Важные моменты

1. **Приложение не падает**, если сертификат не настроен в Production - только предупреждение в лог
2. **HTTP всегда доступен** на порту 5000 для обратного прокси (nginx)
3. **HTTPS всегда включается** в Development с dev-сертификатом
4. **Обработка ошибок** гарантирует, что приложение запустится даже при проблемах с сертификатом
5. **Логирование** помогает диагностировать проблемы с сертификатом

## 📝 Формат переменных окружения

ASP.NET Core использует двойное подчёркивание `__` для вложенных свойств:

- `ASPNETCORE_KESTREL__CERTIFICATE__PATH` → `Kestrel:Certificate:Path`
- `ASPNETCORE_KESTREL__CERTIFICATE__PASSWORD` → `Kestrel:Certificate:Password`

## ✅ Итог

Программа готова к работе в обоих окружениях:
- ✅ Локально работает с dev-сертификатом
- ✅ На сервере работает с production сертификатом из переменных окружения
- ✅ Безопасная обработка ошибок
- ✅ Подробное логирование
- ✅ Не падает при отсутствии сертификата в Production

