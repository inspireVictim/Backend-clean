# Скрипт для проверки, что все работает корректно

Write-Host "=== ПРОВЕРКА СОСТОЯНИЯ СИСТЕМЫ ===" -ForegroundColor Cyan
Write-Host ""

# 1. Проверка PostgreSQL
Write-Host "1. Проверка PostgreSQL..." -ForegroundColor Yellow
$pgRunning = docker ps --filter "name=yess-postgres" --format "{{.Names}}"
if ($pgRunning) {
    Write-Host "   ✅ PostgreSQL запущен: $pgRunning" -ForegroundColor Green
} else {
    Write-Host "   ❌ PostgreSQL не запущен!" -ForegroundColor Red
    exit 1
}

# 2. Проверка таблицы users
Write-Host "`n2. Проверка таблицы 'users'..." -ForegroundColor Yellow
$usersCheck = docker exec yess-postgres psql -U yess_user -d yess_db -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'users';"
$usersExists = [int]($usersCheck -replace '\s+', '')

if ($usersExists -gt 0) {
    Write-Host "   ✅ Таблица 'users' существует!" -ForegroundColor Green
    
    $userCount = docker exec yess-postgres psql -U yess_user -d yess_db -t -c "SELECT COUNT(*) FROM users;"
    $count = [int]($userCount -replace '\s+', '')
    Write-Host "   📊 Количество пользователей: $count" -ForegroundColor Gray
} else {
    Write-Host "   ❌ Таблица 'users' не найдена!" -ForegroundColor Red
    exit 1
}

# 3. Проверка всех таблиц
Write-Host "`n3. Список всех таблиц:" -ForegroundColor Yellow
docker exec yess-postgres psql -U yess_user -d yess_db -c "\dt" | Select-String -Pattern "public"

# 4. Проверка миграций
Write-Host "`n4. Проверка примененных миграций..." -ForegroundColor Yellow
$migrations = docker exec yess-postgres psql -U yess_user -d yess_db -t -c "SELECT COUNT(*) FROM __EFMigrationsHistory;"
$migrationCount = [int]($migrations -replace '\s+', '')
Write-Host "   📋 Применено миграций: $migrationCount" -ForegroundColor Gray

# 5. Проверка backend контейнера
Write-Host "`n5. Проверка backend контейнера..." -ForegroundColor Yellow
$backendRunning = docker ps --filter "name=csharp-backend" --format "{{.Names}}"
if ($backendRunning) {
    Write-Host "   ✅ Backend запущен: $backendRunning" -ForegroundColor Green
} else {
    Write-Host "   ⚠️  Backend не запущен" -ForegroundColor Yellow
}

# 6. Тест подключения
Write-Host "`n6. Тест подключения к БД..." -ForegroundColor Yellow
$testQuery = docker exec yess-postgres psql -U yess_user -d yess_db -t -c "SELECT 'Connection OK' as status;"
if ($testQuery -match "Connection OK") {
    Write-Host "   ✅ Подключение к БД работает!" -ForegroundColor Green
} else {
    Write-Host "   ❌ Проблемы с подключением!" -ForegroundColor Red
}

Write-Host "`n=== ПРОВЕРКА ЗАВЕРШЕНА ===" -ForegroundColor Cyan
Write-Host "`nЕсли все проверки пройдены ✅ - таблица 'users' существует и доступна." -ForegroundColor Green

