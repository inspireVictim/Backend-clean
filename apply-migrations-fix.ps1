# Скрипт для применения миграций с правильными учетными данными
# Исправляет ошибку "password authentication failed for user 'postgres'"

Write-Host "🔍 Проверка состояния базы данных..." -ForegroundColor Cyan

# Проверяем, запущен ли PostgreSQL
$postgresRunning = docker ps --filter "name=yess-postgres" --format "{{.Names}}"
if (-not $postgresRunning) {
    Write-Host "❌ PostgreSQL контейнер не запущен. Запускаю..." -ForegroundColor Red
    docker-compose up -d postgres
    Write-Host "⏳ Ожидание готовности PostgreSQL (10 секунд)..." -ForegroundColor Yellow
    Start-Sleep -Seconds 10
}

# Проверяем существование таблиц
Write-Host "`n📊 Проверка существующих таблиц..." -ForegroundColor Cyan
$tables = docker exec yess-postgres psql -U yess_user -d yess_db -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';"
$tablesCount = [int]($tables -replace '\s+', '')

if ($tablesCount -eq 0) {
    Write-Host "⚠️  Таблицы отсутствуют. Необходимо применить миграции." -ForegroundColor Yellow
} else {
    Write-Host "✅ Найдено таблиц: $tablesCount" -ForegroundColor Green
}

# Проверяем наличие таблицы users
Write-Host "`n🔍 Проверка таблицы 'users'..." -ForegroundColor Cyan
$usersTable = docker exec yess-postgres psql -U yess_user -d yess_db -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users';"
$usersExists = [int]($usersTable -replace '\s+', '')

if ($usersExists -eq 0) {
    Write-Host "❌ Таблица 'users' не найдена!" -ForegroundColor Red
    Write-Host "🚀 Применяю миграции..." -ForegroundColor Yellow
} else {
    Write-Host "✅ Таблица 'users' существует!" -ForegroundColor Green
    Write-Host "`n📋 Список всех таблиц:" -ForegroundColor Cyan
    docker exec yess-postgres psql -U yess_user -d yess_db -c "\dt"
    exit 0
}

# Получаем имя сети
Write-Host "`n🔗 Определение Docker сети..." -ForegroundColor Cyan
$networkName = docker inspect yess-postgres --format='{{range $net,$v := .NetworkSettings.Networks}}{{$net}}{{end}}' | Select-Object -First 1

if ([string]::IsNullOrEmpty($networkName)) {
    Write-Host "⚠️  Не удалось определить сеть. Пробую стандартное имя..." -ForegroundColor Yellow
    $networkName = "yess-backend-dotnet_yess-network"
    
    # Проверяем существование сети
    $networkExists = docker network ls --filter "name=$networkName" --format "{{.Name}}"
    if (-not $networkExists) {
        Write-Host "❌ Сеть '$networkName' не найдена!" -ForegroundColor Red
        Write-Host "Создаю сеть..." -ForegroundColor Yellow
        docker network create $networkName
    }
}

Write-Host "✅ Использую сеть: $networkName" -ForegroundColor Green

# Получаем абсолютный путь к проекту
$projectPath = (Get-Location).Path
if ($projectPath -notmatch "yess-backend-dotnet$") {
    $projectPath = Join-Path $projectPath "yess-backend-dotnet"
}

Write-Host "`n📁 Путь к проекту: $projectPath" -ForegroundColor Cyan

# Применяем миграции с ПРАВИЛЬНЫМИ учетными данными
# ВАЖНО: Используем yess_user, НЕ postgres!
Write-Host "`n🚀 Применение миграций EF Core..." -ForegroundColor Cyan
Write-Host "   Connection: Host=postgres;Database=yess_db;Username=yess_user" -ForegroundColor Gray

$result = docker run --rm `
  --network "$networkName" `
  -v "${projectPath}:/src" `
  -w /src `
  mcr.microsoft.com/dotnet/sdk:8.0 `
  dotnet ef database update `
    --project YessBackend.Infrastructure/YessBackend.Infrastructure.csproj `
    --startup-project YessBackend.Api/YessBackend.Api.csproj `
    --connection "Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password" `
    --verbose 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n✅ Миграции успешно применены!" -ForegroundColor Green
    
    # Проверяем результат
    Write-Host "`n📊 Проверка созданных таблиц..." -ForegroundColor Cyan
    docker exec yess-postgres psql -U yess_user -d yess_db -c "\dt"
    
    Write-Host "`n✅ Таблица 'users' должна быть создана!" -ForegroundColor Green
    $usersCheck = docker exec yess-postgres psql -U yess_user -d yess_db -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users';"
    $usersNow = [int]($usersCheck -replace '\s+', '')
    
    if ($usersNow -gt 0) {
        Write-Host "✅ ПРОВЕРКА ПРОЙДЕНА: Таблица 'users' существует!" -ForegroundColor Green
    }
} else {
    Write-Host "`n❌ Ошибка при применении миграций!" -ForegroundColor Red
    Write-Host $result
    exit 1
}

Write-Host "`n🎉 Готово! Миграции применены, таблицы созданы." -ForegroundColor Green

