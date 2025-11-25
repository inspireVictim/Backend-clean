# 🔧 ИСПРАВЛЕНИЕ: Таблица users не существует

## Проблема
- ❌ `relation "users" does not exist`
- ❌ `password authentication failed for user "postgres"` (при попытке миграций)

## Причина
Миграции не применены. Connection string использует неправильного пользователя (`postgres` вместо `yess_user`).

---

## ✅ РЕШЕНИЕ - Применить миграции с правильными учетными данными

### ШАГ 1: Запустить PostgreSQL

```powershell
cd E:\YessProjectCsharp\yess-backend-dotnet
docker-compose up -d postgres
```

Подождите 5-10 секунд, пока PostgreSQL запустится.

### ШАГ 2: Проверить состояние БД

```powershell
# Проверить запущен ли контейнер
docker ps --filter "name=yess-postgres"

# Проверить готовность
docker exec yess-postgres pg_isready -U yess_user -d yess_db

# Проверить существующие таблицы
docker exec yess-postgres psql -U yess_user -d yess_db -c "\dt"
```

### ШАГ 3: Применить миграции

**Вариант A: Использовать готовый скрипт (РЕКОМЕНДУЕТСЯ)**

```powershell
cd E:\YessProjectCsharp\yess-backend-dotnet
.\apply-migrations-fix.ps1
```

**Вариант B: Применить вручную**

```powershell
# Получить имя сети
$networkName = docker inspect yess-postgres --format='{{range $net,$v := .NetworkSettings.Networks}}{{$net}}{{end}}' | Select-Object -First 1

# Применить миграции (ВАЖНО: используется yess_user, НЕ postgres!)
docker run --rm `
  --network "$networkName" `
  -v "E:\YessProjectCsharp\yess-backend-dotnet:/src" `
  -w /src `
  mcr.microsoft.com/dotnet/sdk:8.0 `
  dotnet ef database update `
    --project YessBackend.Infrastructure/YessBackend.Infrastructure.csproj `
    --startup-project YessBackend.Api/YessBackend.Api.csproj `
    --connection "Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password"
```

### ШАГ 4: Проверить результат

```powershell
# Проверить все таблицы
docker exec yess-postgres psql -U yess_user -d yess_db -c "\dt"

# Проверить конкретно таблицу users
docker exec yess-postgres psql -U yess_user -d yess_db -c "SELECT COUNT(*) FROM users;"

# Проверить историю миграций
docker exec yess-postgres psql -U yess_user -d yess_db -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"
```

### ШАГ 5: Запустить backend

```powershell
docker-compose up -d
```

### ШАГ 6: Проверить работу backend

```powershell
# Проверить логи
docker-compose logs -f csharp-backend

# Проверить health endpoint
curl http://localhost:8000/health
```

---

## 🔑 КЛЮЧЕВЫЕ МОМЕНТЫ

### Правильные учетные данные для миграций:

```
Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password
```

❌ **НЕПРАВИЛЬНО:**
```
Username=postgres  # ❌ Пользователь 'postgres' не существует!
```

✅ **ПРАВИЛЬНО:**
```
Username=yess_user  # ✅ Пользователь создан в docker-compose.yml
```

### Почему возникает ошибка "password authentication failed for user postgres"?

Когда вы запускаете `dotnet ef database update` **локально** (не в Docker), EF Core пытается использовать connection string из `appsettings.json`, где указан `Username=postgres` (если там так написано) или дефолтные настройки.

**Решение:** Всегда явно указывайте `--connection` с правильными учетными данными, как в примерах выше.

---

## 📋 ОЖИДАЕМЫЙ РЕЗУЛЬТАТ

После применения миграций вы должны увидеть:

```
✅ Таблицы созданы:
- __EFMigrationsHistory
- users
- wallets
- transactions
- partners
- orders
- notifications
- и другие...
```

---

## 🐛 ДИАГНОСТИКА

### Если миграции не применяются:

1. **Проверьте, что PostgreSQL запущен:**
   ```powershell
   docker ps --filter "name=yess-postgres"
   ```

2. **Проверьте учетные данные:**
   ```powershell
   docker exec yess-postgres env | grep POSTGRES
   ```
   Должно быть:
   ```
   POSTGRES_USER=yess_user
   POSTGRES_PASSWORD=secure_password
   POSTGRES_DB=yess_db
   ```

3. **Проверьте, что сеть существует:**
   ```powershell
   docker network ls | grep yess-network
   ```

4. **Проверьте подключение вручную:**
   ```powershell
   docker exec -it yess-postgres psql -U yess_user -d yess_db -c "SELECT version();"
   ```

---

## ✅ ИТОГОВАЯ ПРОВЕРКА

После применения миграций:

1. ✅ Таблица `users` существует
2. ✅ Таблица `__EFMigrationsHistory` содержит запись о миграции
3. ✅ Backend запускается без ошибок
4. ✅ Нет ошибок "relation does not exist"

**Готово!** 🎉

