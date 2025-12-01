# Команды для пересборки контейнера бэкенда

## Быстрая пересборка (рекомендуется)
Пересобирает только бэкенд, не затрагивая другие сервисы (PostgreSQL, Redis):

```bash
sudo docker-compose build --no-cache csharp-backend
sudo docker-compose up -d --no-deps csharp-backend
```

## Пересборка с остановкой и запуском всех сервисов

```bash
sudo docker-compose down
sudo docker-compose build --no-cache csharp-backend
sudo docker-compose up -d
```

## Пересборка с очисткой старых образов

```bash
# Остановка контейнера
sudo docker-compose stop csharp-backend

# Удаление старого образа
sudo docker rmi $(sudo docker images -q yess-backend-dotnet_csharp-backend) 2>/dev/null || true

# Пересборка
sudo docker-compose build --no-cache csharp-backend
sudo docker-compose up -d --no-deps csharp-backend
```

## Полная пересборка с очисткой всех неиспользуемых образов

```bash
# Остановка всех контейнеров
sudo docker-compose down

# Очистка неиспользуемых образов (опционально)
sudo docker system prune -f

# Пересборка
sudo docker-compose build --no-cache csharp-backend
sudo docker-compose up -d
```

## Просмотр логов после пересборки

```bash
# Последние 50 строк
sudo docker-compose logs --tail=50 csharp-backend

# Логи в реальном времени
sudo docker-compose logs -f csharp-backend
```

## Проверка работы API

```bash
# Health check
curl http://localhost:8000/api/v1/health

# Или через Swagger
curl http://localhost:8000/swagger
```

## Использование готового скрипта

```bash
chmod +x REBUILD_BACKEND.sh
./REBUILD_BACKEND.sh
```

## Объяснение параметров

- `--no-cache` - пересобирает образ без использования кэша Docker (гарантирует свежую сборку)
- `--no-deps` - запускает только указанный сервис, не перезапуская зависимости
- `-d` - запуск в фоновом режиме (detached mode)


