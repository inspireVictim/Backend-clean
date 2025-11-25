# PowerShell скрипт для применения миграций EF Core к PostgreSQL в Docker

Write-Host "🚀 Применение миграций EF Core..." -ForegroundColor Cyan

# Проверяем, что PostgreSQL запущен
$postgresRunning = docker ps --filter "name=yess-postgres" --format "{{.Names}}" | Select-String -Pattern "yess-postgres"

if (-not $postgresRunning) {
    Write-Host "❌ PostgreSQL контейнер не запущен. Запускаю..." -ForegroundColor Yellow
    docker-compose up -d postgres
    Write-Host "⏳ Ожидание готовности PostgreSQL..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
}

# Получаем имя сети
$networkName = docker inspect yess-postgres --format='{{range $net,$v := .NetworkSettings.Networks}}{{$net}}{{end}}' | Select-Object -First 1

if ([string]::IsNullOrEmpty($networkName)) {
    Write-Host "❌ Не удалось определить сеть. Используем docker-compose сеть..." -ForegroundColor Yellow
    $networkName = "yess-backend-dotnet_yess-network"
}

Write-Host "📦 Использую сеть: $networkName" -ForegroundColor Cyan

# Получаем текущую директорию
$currentDir = Get-Location

# Применяем миграции через SDK контейнер
docker run --rm `
  --network "$networkName" `
  -v "${currentDir}:/src" `
  -w /src `
  mcr.microsoft.com/dotnet/sdk:8.0 `
  dotnet ef database update `
    --project YessBackend.Infrastructure/YessBackend.Infrastructure.csproj `
    --startup-project YessBackend.Api/YessBackend.Api.csproj `
    --connection "Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password"

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Миграции успешно применены!" -ForegroundColor Green
} else {
    Write-Host "❌ Ошибка при применении миграций!" -ForegroundColor Red
    exit 1
}

