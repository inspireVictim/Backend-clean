# ⚡ Простая настройка после создания сертификата

## ✅ Сертификат создан!

Сертификат успешно создан: `/etc/ssl/certs/yess-cert.pfx`

## 🚀 Быстрая настройка (выберите один вариант)

### Вариант A: Автоматический (рекомендуется)

```bash
# Сделайте скрипт исполняемым
sudo chmod +x setup_service_quick.sh

# Запустите скрипт (он найдёт пути автоматически)
sudo ./setup_service_quick.sh
```

### Вариант B: Вручную (если приложение уже запущено)

Если приложение уже работает, просто остановите его и запустите заново с переменными окружения:

```bash
# 1. Найдите процесс
ps aux | grep dotnet

# 2. Остановите (Ctrl+C или kill)
# Или если в screen/tmux:
screen -r  # или tmux attach

# 3. Установите переменные и запустите
export ASPNETCORE_ENVIRONMENT=Production
export SSL_CERT_PATH=/etc/ssl/certs/yess-cert.pfx
export SSL_CERT_PASSWORD=YesSGo!@#!

cd ~/Backend/YessBackend.Api
dotnet YessBackend.Api.dll
```

### Вариант C: Создать systemd service вручную

```bash
# 1. Найдите пути
find ~ -name "YessBackend.Api.dll"
which dotnet

# 2. Создайте service файл
sudo nano /etc/systemd/system/yess-backend.service
```

Вставьте (замените пути на ваши):

```ini
[Unit]
Description=Yess Backend API
After=network.target

[Service]
Type=notify
WorkingDirectory=/home/yesgoadm/Backend/YessBackend.Api
ExecStart=/usr/bin/dotnet /home/yesgoadm/Backend/YessBackend.Api/YessBackend.Api.dll
Restart=always
RestartSec=10
User=yesgoadm
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=SSL_CERT_PATH=/etc/ssl/certs/yess-cert.pfx
Environment=SSL_CERT_PASSWORD=YesSGo!@#!

[Install]
WantedBy=multi-user.target
```

```bash
# 3. Активируйте
sudo systemctl daemon-reload
sudo systemctl enable yess-backend
sudo systemctl start yess-backend
sudo systemctl status yess-backend
```

## 🔍 Проверка

```bash
# Проверьте порт
sudo netstat -tlnp | grep 8443

# Проверьте HTTPS
curl -vk https://localhost:8443/health

# Проверьте логи (если systemd)
sudo journalctl -u yess-backend -f
```

## ✅ Готово!

Если порт 8443 слушается - всё работает! 🎉

