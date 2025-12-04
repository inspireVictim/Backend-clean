#!/bin/bash
# Быстрый скрипт для создания systemd service
# Использование: sudo ./setup_service_quick.sh

set -e

SERVICE_NAME="yess-backend"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"
APP_USER="yesgoadm"
APP_GROUP="yesgoadm"

# Определяем пути
echo "🔍 Поиск путей к приложению..."

# Находим YessBackend.Api.dll
DLL_PATH=$(find /home/$APP_USER -name "YessBackend.Api.dll" 2>/dev/null | head -n 1)

if [ -z "$DLL_PATH" ]; then
    echo "❌ Ошибка: Не найден YessBackend.Api.dll"
    echo "Укажите путь вручную:"
    read -p "Путь к YessBackend.Api.dll: " DLL_PATH
fi

# Находим директорию приложения
APP_DIR=$(dirname "$DLL_PATH")
echo "📁 Директория приложения: $APP_DIR"

# Находим dotnet
DOTNET_PATH=$(which dotnet)
if [ -z "$DOTNET_PATH" ]; then
    echo "❌ Ошибка: dotnet не найден в PATH"
    echo "Укажите путь вручную:"
    read -p "Путь к dotnet: " DOTNET_PATH
fi
echo "🔧 Dotnet: $DOTNET_PATH"

# Проверяем, существует ли service файл
if [ -f "$SERVICE_FILE" ]; then
    echo "⚠️  Service файл уже существует: $SERVICE_FILE"
    read -p "Перезаписать? (y/n): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Отменено."
        exit 1
    fi
fi

# Создаём service файл
echo "📝 Создание service файла..."
sudo tee "$SERVICE_FILE" > /dev/null <<EOF
[Unit]
Description=Yess Backend API
After=network.target

[Service]
Type=notify
WorkingDirectory=$APP_DIR
ExecStart=$DOTNET_PATH $DLL_PATH
Restart=always
RestartSec=10
SyslogIdentifier=$SERVICE_NAME
User=$APP_USER
Group=$APP_GROUP
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:8000
Environment=SSL_CERT_PATH=/etc/ssl/certs/yess-cert.pfx
Environment=SSL_CERT_PASSWORD=YesSGo!@#!
StandardOutput=journal
StandardError=journal

[Install]
WantedBy=multi-user.target
EOF

echo "✅ Service файл создан: $SERVICE_FILE"

# Перезагружаем systemd
echo "🔄 Перезагрузка systemd..."
sudo systemctl daemon-reload

# Включаем автозапуск
echo "✅ Включение автозапуска..."
sudo systemctl enable $SERVICE_NAME

# Запускаем service
echo "🚀 Запуск service..."
sudo systemctl start $SERVICE_NAME

# Ждём немного
sleep 2

# Проверяем статус
echo ""
echo "📊 Статус service:"
sudo systemctl status $SERVICE_NAME --no-pager -l

echo ""
echo "✅ Готово!"
echo ""
echo "📋 Полезные команды:"
echo "   sudo systemctl status $SERVICE_NAME    # Статус"
echo "   sudo systemctl restart $SERVICE_NAME   # Перезапуск"
echo "   sudo journalctl -u $SERVICE_NAME -f    # Логи"
echo ""
echo "🔍 Проверка порта 8443:"
sudo netstat -tlnp | grep 8443 || echo "⚠️  Порт 8443 не слушается. Проверьте логи."

