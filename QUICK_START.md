# 🚀 БЫСТРОЕ ИСПРАВЛЕНИЕ - Команды по порядку

## ⚡ ОДНОЙ КОМАНДОЙ (Linux/Mac/WSL)

```bash
cd yess-backend-dotnet
./quick-fix.sh
```

---

## 📝 ПОШАГОВО (если нужна ручная настройка)

### 1. Остановить и очистить всё

```bash
cd yess-backend-dotnet
docker-compose down -v
```

### 2. Запустить только PostgreSQL

```bash
docker-compose up -d postgres
```

### 3. Подождать готовности PostgreSQL (5-10 секунд)

```bash
# Проверить готовность
docker exec yess-postgres pg_isready -U yess_user -d yess_db

# Если готов - увидите: yess-postgres:5432 - accepting connections
```

### 4. Применить миграции

**Linux/Mac/WSL:**
```bash
./apply-migrations.sh
```

**Windows PowerShell:**
```powershell
.\apply-migrations.ps1
```

**Или вручную:**
```bash
docker run --rm \
  --network yess-backend-dotnet_yess-network \
  -v "$(pwd):/src" -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet ef database update \
    --project YessBackend.Infrastructure/YessBackend.Infrastructure.csproj \
    --startup-project YessBackend.Api/YessBackend.Api.csproj \
    --connection "Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password"
```

### 5. Проверить таблицы

```bash
docker exec -it yess-postgres psql -U yess_user -d yess_db -c "\dt"
```

### 6. Запустить все сервисы

```bash
docker-compose up -d
```

### 7. Проверить логи

```bash
docker-compose logs -f csharp-backend
```

---

## ✅ ПРОВЕРКА УСПЕШНОСТИ

```bash
# 1. Проверить таблицы в БД
docker exec -it yess-postgres psql -U yess_user -d yess_db -c "\dt"

# Должны увидеть: users, wallets, transactions, partners, orders и т.д.

# 2. Проверить health endpoint
curl http://localhost:8000/health
curl http://localhost:8000/api/v1/health

# 3. Проверить статус контейнеров
docker-compose ps

# Все должны быть "Up"
```

---

## 🔍 ДИАГНОСТИКА ОШИБОК

### Ошибка: "password authentication failed"

**Проверка:**
```bash
# Проверить переменные окружения в контейнере PostgreSQL
docker exec yess-postgres env | grep POSTGRES

# Должно быть:
# POSTGRES_USER=yess_user
# POSTGRES_PASSWORD=secure_password
# POSTGRES_DB=yess_db
```

**Исправление:** Убедитесь, что в `docker-compose.yml` совпадают:
- `POSTGRES_USER: yess_user`
- `POSTGRES_PASSWORD: secure_password`
- В connection string: `Username=yess_user;Password=secure_password`

### Ошибка: "relation 'users' does not exist"

**Решение:** Миграции не применены. Выполните ШАГ 4.

### Ошибка: "could not translate host name 'postgres'"

**Решение:** Backend должен запускаться через `docker-compose up`, не `dotnet run`. Контейнеры должны быть в одной сети.

### Ошибка: "dotnet ef: command not found"

**Решение:** Используйте скрипт `apply-migrations.sh` или команду через SDK контейнер (ШАГ 4).

---

## 📋 ВАЖНЫЕ ПАРАМЕТРЫ

**Connection String в Docker:**
```
Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password
```

**Параметры PostgreSQL:**
- User: `yess_user`
- Password: `secure_password`
- Database: `yess_db`
- Host (внутри Docker): `postgres` (имя сервиса)
- Host (снаружи Docker): `localhost`

---

## 🎯 ИТОГОВАЯ ПРОВЕРКА

После выполнения всех шагов:

1. ✅ PostgreSQL запущен и принимает подключения
2. ✅ Таблицы созданы (users, wallets, transactions и т.д.)
3. ✅ Backend контейнер запущен без ошибок
4. ✅ Health endpoint отвечает
5. ✅ Логи без ошибок подключения к БД

**Готово!** 🎉

