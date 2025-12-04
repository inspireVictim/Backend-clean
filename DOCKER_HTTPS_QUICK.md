# 🚀 Быстрая настройка HTTPS для Docker - 3 шага

## ✅ Что уже готово

Все файлы настроены:
- ✅ `Program.cs` - поддержка HTTPS
- ✅ `docker-compose.yml` - проброс портов
- ✅ Скрипты создания сертификата

## 📋 Что нужно сделать (3 шага)

### Шаг 1: Создайте сертификат

**Linux/Mac:**
```bash
chmod +x setup_cert_for_docker.sh
./setup_cert_for_docker.sh
```

**Windows:**
```powershell
.\setup_cert_for_docker.ps1
```

### Шаг 2: Активируйте HTTPS в docker-compose.yml

Откройте `docker-compose.yml` и раскомментируйте **3 строки**:

```yaml
environment:
  # Раскомментируйте эти 2 строки:
  - ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PATH=/etc/ssl/certs/yess-cert.pfx
  - ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PASSWORD=YesSGo!@#!

volumes:
  # Раскомментируйте эту строку:
  - ./certs:/etc/ssl/certs:ro
```

### Шаг 3: Перезапустите контейнеры

```bash
docker-compose down
docker-compose up -d
```

### Проверка:

```bash
# HTTP
curl http://localhost:8000/health

# HTTPS
curl -vk https://localhost:8443/health
```

## 🎯 Готово!

- ✅ HTTP: порт **8000**
- ✅ HTTPS: порт **8443**
- ✅ Swagger: `/docs`

## 📖 Подробная инструкция

Если нужны детали: `DOCKER_HTTPS_SETUP.md`

