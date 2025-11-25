# ИСПРАВЛЕНИЕ ОШИБОК ПОДКЛЮЧЕНИЯ К POSTGRES И МИГРАЦИЙ

## Проблема
- `password authentication failed for user 'postgres'`
- `relation 'users' does not exist`

---

## ШАГ 1: Исправление docker-compose.yml

Убедитесь, что в `docker-compose.yml` совпадают пароли и имена пользователей:

```yaml
version: "3.9"

services:
  postgres:
    image: postgres:15
    container_name: yess-postgres
    restart: unless-stopped
    environment:
      POSTGRES_USER: yess_user           # ✅ Должно совпадать с connection string
      POSTGRES_PASSWORD: secure_password # ✅ Должно совпадать с connection string
      POSTGRES_DB: yess_db               # ✅ Должно совпадать с connection string
    ports:
      - "5432:5432"
    volumes:
      - yess_pg_data:/var/lib/postgresql/data
    networks:
      - yess-network
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U yess_user -d yess_db"]
      interval: 5s
      timeout: 5s
      retries: 5

  redis:
    image: redis:7-alpine
    container_name: yess-redis
    restart: unless-stopped
    ports:
      - "6379:6379"
    networks:
      - yess-network

  csharp-backend:
    build:
      context: .
      dockerfile: Dockerfile
    container_name: csharp-backend
    restart: unless-stopped
    depends_on:
      postgres:
        condition: service_healthy  # ✅ Ждем готовности БД
      redis:
        condition: service_started
    ports:
      - "8000:8000"
      - "8443:8443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - EnableSwagger=true
      # ✅ ИСПРАВЛЕНО: Используем имя сервиса 'postgres' вместо localhost
      - ConnectionStrings__DefaultConnection=Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password
      - Redis__ConnectionString=redis:6379
      - Jwt__SecretKey=your_secret_key_here_change_in_production
      - Jwt__Issuer=YessBackend
      - Jwt__Audience=YessUsers
      - Jwt__AccessTokenExpireMinutes=30
      - Jwt__RefreshTokenExpireMinutes=10080
      - Cors__Origins=["http://localhost:3000","http://localhost:3001","http://localhost:3002","http://127.0.0.1:3000","http://127.0.0.1:3001","http://127.0.0.1:3002"]
    networks:
      - yess-network

volumes:
  yess_pg_data:

networks:
  yess-network:
    driver: bridge
```

---

## ШАГ 2: Остановка и очистка старых контейнеров

```bash
cd yess-backend-dotnet

# Остановить все контейнеры
docker-compose down

# Удалить старые volumes (если база была создана с неправильным паролем)
docker volume rm yess-backend-dotnet_yess_pg_data

# ИЛИ удалить все volumes проекта
docker-compose down -v
```

---

## ШАГ 3: Запуск PostgreSQL и ожидание готовности

```bash
# Запустить только PostgreSQL
docker-compose up -d postgres

# Проверить логи и готовность
docker-compose logs postgres

# Проверить подключение к БД
docker exec -it yess-postgres psql -U yess_user -d yess_db -c "SELECT version();"
```

Ожидаемый результат:
```
PostgreSQL 15.x on x86_64...
```

---

## ШАГ 4: Применение миграций EF Core через Docker

### Вариант A: Применить миграции изнутри контейнера backend

```bash
# 1. Собрать образ backend (без запуска)
docker-compose build csharp-backend

# 2. Применить миграции через временный контейнер
docker-compose run --rm csharp-backend dotnet ef database update --project /src/YessBackend.Infrastructure --startup-project /src/YessBackend.Api --connection "Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password"
```

**Проблема:** EF Core tools могут отсутствовать в runtime образе.

---

### Вариант B: Применить миграции через build-контейнер (РЕКОМЕНДУЕТСЯ)

```bash
# 1. Создать скрипт для миграций
cat > apply-migrations.sh << 'EOF'
#!/bin/bash
docker run --rm \
  --network yess-backend-dotnet_yess-network \
  -v "$(pwd):/src" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet ef database update \
    --project YessBackend.Infrastructure/YessBackend.Infrastructure.csproj \
    --startup-project YessBackend.Api/YessBackend.Api.csproj \
    --connection "Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password"
EOF

chmod +x apply-migrations.sh

# 2. Запустить скрипт
./apply-migrations.sh
```

---

### Вариант C: Применить миграции через docker-compose exec (если контейнер уже запущен)

```bash
# 1. Запустить backend контейнер
docker-compose up -d csharp-backend

# 2. Войти в контейнер и применить миграции
docker exec -it csharp-backend bash

# Внутри контейнера (если есть dotnet ef):
dotnet ef database update --project /src/YessBackend.Infrastructure --startup-project /src/YessBackend.Api

# Выйти
exit
```

---

### Вариант D: Использовать автоматическое применение миграций (уже настроено в Program.cs)

Если миграции применяются автоматически при старте (строки 308-399 в Program.cs), просто запустите:

```bash
# Запустить все сервисы
docker-compose up -d

# Проверить логи backend на предмет ошибок миграций
docker-compose logs -f csharp-backend
```

---

## ШАГ 5: Проверка успешного применения миграций

```bash
# 1. Проверить наличие таблиц
docker exec -it yess-postgres psql -U yess_user -d yess_db -c "\dt"

# Ожидаемый результат: список таблиц
# - __EFMigrationsHistory
# - users
# - wallets
# - transactions
# - partners
# - orders
# и т.д.

# 2. Проверить историю миграций
docker exec -it yess-postgres psql -U yess_user -d yess_db -c "SELECT * FROM \"__EFMigrationsHistory\" ORDER BY \"MigrationId\";"

# 3. Проверить таблицу users
docker exec -it yess-postgres psql -U yess_user -d yess_db -c "SELECT COUNT(*) FROM users;"
```

---

## ШАГ 6: Запуск всех сервисов

```bash
# Запустить все сервисы
docker-compose up -d

# Проверить статус
docker-compose ps

# Проверить логи
docker-compose logs -f csharp-backend
```

---

## БЫСТРОЕ РЕШЕНИЕ (одной командой)

```bash
cd yess-backend-dotnet

# Остановить и очистить
docker-compose down -v

# Запустить PostgreSQL
docker-compose up -d postgres

# Подождать 5 секунд
sleep 5

# Применить миграции через SDK контейнер
docker run --rm \
  --network yess-backend-dotnet_yess-network \
  -v "$(pwd):/src" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet ef database update \
    --project YessBackend.Infrastructure/YessBackend.Infrastructure.csproj \
    --startup-project YessBackend.Api/YessBackend.Api.csproj \
    --connection "Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password"

# Запустить все сервисы
docker-compose up -d

# Проверить логи
docker-compose logs -f csharp-backend
```

---

## ПРОВЕРКА УСПЕШНОСТИ

1. ✅ Backend запущен без ошибок:
   ```bash
   docker-compose logs csharp-backend | grep -i "error\|exception"
   ```

2. ✅ Health check работает:
   ```bash
   curl http://localhost:8000/health
   curl http://localhost:8000/api/v1/health
   ```

3. ✅ База данных содержит таблицы:
   ```bash
   docker exec -it yess-postgres psql -U yess_user -d yess_db -c "\dt" | grep users
   ```

---

## ТИПИЧНЫЕ ОШИБКИ И РЕШЕНИЯ

### Ошибка: "password authentication failed"
**Решение:** Убедитесь, что в docker-compose.yml пароли совпадают:
- `POSTGRES_PASSWORD` = `secure_password`
- В connection string: `Password=secure_password`

### Ошибка: "relation 'users' does not exist"
**Решение:** Миграции не применены. Используйте ШАГ 4.

### Ошибка: "could not translate host name 'postgres'"
**Решение:** Backend запущен вне Docker сети. Используйте `docker-compose up`, не `dotnet run`.

### Ошибка: "dotnet ef: command not found"
**Решение:** Используйте Вариант B из ШАГ 4 (через SDK контейнер).

---

## ИЗМЕНЕНИЕ ПАРОЛЯ (если нужно)

1. Измените в `docker-compose.yml`:
   ```yaml
   POSTGRES_PASSWORD: новый_пароль
   ConnectionStrings__DefaultConnection: ...Password=новый_пароль
   ```

2. Удалите volume и пересоздайте:
   ```bash
   docker-compose down -v
   docker-compose up -d
   ```

