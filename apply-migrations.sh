#!/bin/bash
# Скрипт для применения миграций EF Core к PostgreSQL в Docker

set -e

echo "🚀 Применение миграций EF Core..."

# Проверяем, что PostgreSQL запущен
if ! docker ps | grep -q yess-postgres; then
    echo "❌ PostgreSQL контейнер не запущен. Запускаю..."
    docker-compose up -d postgres
    echo "⏳ Ожидание готовности PostgreSQL..."
    sleep 5
fi

# Получаем имя сети
NETWORK_NAME=$(docker inspect yess-postgres --format='{{range $net,$v := .NetworkSettings.Networks}}{{$net}}{{end}}' | head -1)

if [ -z "$NETWORK_NAME" ]; then
    echo "❌ Не удалось определить сеть. Используем docker-compose сеть..."
    NETWORK_NAME="yess-backend-dotnet_yess-network"
fi

echo "📦 Использую сеть: $NETWORK_NAME"

# Применяем миграции через SDK контейнер
docker run --rm \
  --network "$NETWORK_NAME" \
  -v "$(pwd):/src" \
  -w /src \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet ef database update \
    --project YessBackend.Infrastructure/YessBackend.Infrastructure.csproj \
    --startup-project YessBackend.Api/YessBackend.Api.csproj \
    --connection "Host=postgres;Port=5432;Database=yess_db;Username=yess_user;Password=secure_password"

echo "✅ Миграции успешно применены!"

