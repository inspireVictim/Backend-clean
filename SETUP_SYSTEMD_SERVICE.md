# 🔧 Настройка systemd service для Yess Backend

## 📋 Проверка текущего статуса

Сначала проверьте, как сейчас запущено приложение:

```bash
# Проверьте, есть ли процесс
ps aux | grep dotnet

# Проверьте, есть ли другие сервисы
systemctl list-units | grep yess
```

## 🚀 Вариант 1: Создание нового systemd service

### Шаг 1: Создайте service файл

```bash
sudo nano /etc/systemd/system/yess-backend.service
```

### Шаг 2: Добавьте содержимое

**Если приложение запускается напрямую через dotnet:**

```ini
[Unit]
Description=Yess Backend API
After=network.target postgresql.service

[Service]
Type=notify
WorkingDirectory=/home/yesgoadm/Backend/YessBackend.Api
ExecStart=/usr/bin/dotnet /home/yesgoadm/Backend/YessBackend.Api/YessBackend.Api.dll
Restart=always
RestartSec=10
SyslogIdentifier=yess-backend
User=yesgoadm
Group=yesgoadm
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:8000
Environment=SSL_CERT_PATH=/etc/ssl/certs/yess-cert.pfx
Environment=SSL_CERT_PASSWORD=YesSGo!@#!
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
```

**Если используете Docker:**

```ini
[Unit]
Description=Yess Backend API (Docker)
After=docker.service
Requires=docker.service

[Service]
Type=notify
ExecStart=/usr/bin/docker-compose -f /home/yesgoadm/Backend/docker-compose.yml up
ExecStop=/usr/bin/docker-compose -f /home/yesgoadm/Backend/docker-compose.yml down
Restart=always
RestartSec=10
SyslogIdentifier=yess-backend
User=yesgoadm
Group=docker

[Install]
WantedBy=multi-user.target
```

### Шаг 3: Обновите пути

**Важно**: Замените пути на реальные:
- `/home/yesgoadm/Backend/YessBackend.Api` - путь к вашему приложению
- `/usr/bin/dotnet` - путь к dotnet (проверьте командой `which dotnet`)

Проверьте путь к приложению:
```bash
# Найдите где находится YessBackend.Api.dll
find /home/yesgoadm -name "YessBackend.Api.dll" 2>/dev/null

# Проверьте путь к dotnet
which dotnet
```

### Шаг 4: Активируйте и запустите service

```bash
# Перезагрузите systemd
sudo systemctl daemon-reload

# Включите автозапуск
sudo systemctl enable yess-backend

# Запустите сервис
sudo systemctl start yess-backend

# Проверьте статус
sudo systemctl status yess-backend

# Проверьте логи
sudo journalctl -u yess-backend -f
```

---

## 🔄 Вариант 2: Если приложение уже запущено (обновление существующего)

### Если используете другой способ запуска (screen/tmux/nohup):

1. **Найдите процесс:**
```bash
ps aux | grep dotnet
```

2. **Остановите приложение** (Ctrl+C или kill)

3. **Запустите с переменными окружения:**
```bash
export ASPNETCORE_ENVIRONMENT=Production
export SSL_CERT_PATH=/etc/ssl/certs/yess-cert.pfx
export SSL_CERT_PASSWORD=YesSGo!@#!

cd /home/yesgoadm/Backend/YessBackend.Api
dotnet YessBackend.Api.dll
```

---

## 🐳 Вариант 3: Если используете Docker

### Обновите docker-compose.yml:

```yaml
services:
  api:
    image: yess-backend:latest
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - SSL_CERT_PATH=/etc/ssl/certs/yess-cert.pfx
      - SSL_CERT_PASSWORD=YesSGo!@#!
    volumes:
      - /etc/ssl/certs:/etc/ssl/certs:ro
    ports:
      - "8000:8000"
      - "8443:8443"
    restart: unless-stopped
```

Затем:
```bash
docker-compose up -d
```

---

## 🔍 Проверка

### 1. Проверьте переменные окружения в процессе:

```bash
# Если systemd service
sudo systemctl show yess-backend | grep Environment

# Если Docker
docker exec <container_name> env | grep SSL
```

### 2. Проверьте, что порт слушается:

```bash
sudo netstat -tlnp | grep 8443
# или
sudo ss -tlnp | grep 8443
```

### 3. Проверьте логи:

```bash
# systemd
sudo journalctl -u yess-backend -n 50 -f

# Docker
docker-compose logs -f api

# Должно быть:
# Now listening on: https://0.0.0.0:8443
```

### 4. Тестирование HTTPS:

```bash
curl -vk https://localhost:8443/health
curl -vk https://5.59.232.211:8443/health
```

---

## 📝 Полезные команды systemd

```bash
# Статус
sudo systemctl status yess-backend

# Логи
sudo journalctl -u yess-backend -f
sudo journalctl -u yess-backend -n 100

# Перезапуск
sudo systemctl restart yess-backend

# Остановка
sudo systemctl stop yess-backend

# Запуск
sudo systemctl start yess-backend

# Отключить автозапуск
sudo systemctl disable yess-backend

# Включить автозапуск
sudo systemctl enable yess-backend
```

---

## ⚠️ Проблемы и решения

### Проблема: Permission denied

**Решение**: Проверьте права доступа к сертификату
```bash
sudo chmod 644 /etc/ssl/certs/yess-cert.pfx
sudo chown yesgoadm:yesgoadm /etc/ssl/certs/yess-cert.pfx
```

### Проблема: Порт уже используется

**Решение**: Найдите процесс и остановите его
```bash
sudo lsof -i :8443
sudo kill <PID>
```

### Проблема: Переменные окружения не применяются

**Решение**: Убедитесь, что они в секции `[Service]` и перезагрузите systemd
```bash
sudo systemctl daemon-reload
sudo systemctl restart yess-backend
```

---

## ✅ Быстрая команда для проверки

```bash
# Создать service и запустить (после настройки путей в файле)
sudo nano /etc/systemd/system/yess-backend.service
# Вставьте конфигурацию выше
sudo systemctl daemon-reload
sudo systemctl enable yess-backend
sudo systemctl start yess-backend
sudo systemctl status yess-backend
```

