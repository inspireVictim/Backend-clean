#!/bin/bash
# Быстрое исправление: очистка, запуск PostgreSQL, применение миграций, запуск всех сервисов

set -e

echo "🔧 Быстрое исправление базы данных и миграций..."
echo ""

cd "$(dirname "$0")"

# ШАГ 1: Остановка и очистка
echo "1️⃣  Остановка и очистка старых контейнеров..."
docker-compose down -v

# ШАГ 2: Запуск PostgreSQL
echo ""
echo "2️⃣  Запуск PostgreSQL..."
docker-compose up -d postgres

# ШАГ 3: Ожидание готовности PostgreSQL
echo ""
echo "3️⃣  Ожидание готовности PostgreSQL..."
for i in {1..30}; do
    if docker exec yess-postgres pg_isready -U yess_user -d yess_db > /dev/null 2>&1; then
        echo "✅ PostgreSQL готов!"
        break
    fi
    if [ $i -eq 30 ]; then
        echo "❌ PostgreSQL не запустился за 30 секунд"
        exit 1
    fi
    sleep 1
done

# ШАГ 4: Применение миграций
echo ""
echo "4️⃣  Применение миграций EF Core..."

# Получаем имя сети
NETWORK_NAME=$(docker inspect yess-postgres --format='{{range $net,$v := .NetworkSettings.Networks}}{{$net}}{{end}}' | head -1)

if [ -z "$NETWORK_NAME" ]; then
    NETWORK_NAME="yess-backend-dotnet_yess-network"
fi

docker run --rm \
  --network "$NETWORK_NAME" \
  -v "$(pwd):/src" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet ef database update \
    --project YessBackend.Infrastructure/YessBackend.Infrastructure.csproj \
    --startup-project YessBackend.Api/YessBackend.Api.csproj \
    --connection "Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password"

echo "✅ Миграции применены!"

# ШАГ 5: Проверка таблиц
echo ""
echo "5️⃣  Проверка созданных таблиц..."
docker exec -it yess-postgres psql -U yess_user -d yess_db -c "\dt" | head -20

# ШАГ 6: Запуск всех сервисов
echo ""
echo "6️⃣  Запуск всех сервисов..."
docker-compose up -d

# ШАГ 7: Ожидание запуска backend
echo ""
echo "7️⃣  Ожидание запуска backend..."
sleep 3

# ШАГ 8: Проверка логов
echo ""
echo "8️⃣  Проверка статуса..."
docker-compose ps

echo ""
echo "✅ Готово! Проверьте логи:"
echo "   docker-compose logs -f csharp-backend"
echo ""
echo "Проверьте health endpoint:"
echo "   curl http://localhost:8000/health"

