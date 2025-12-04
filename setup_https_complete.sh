#!/bin/bash
# Полная настройка HTTPS для Yess Backend
# Использование: sudo ./setup_https_complete.sh

set -e

CERT_DIR="/etc/ssl/certs"
KEY_DIR="/etc/ssl/private"
CERT_NAME="yess-cert"
PASSWORD="YesSGo!@#!"
IP_ADDRESS="5.59.232.211"
CERT_PATH="$CERT_DIR/$CERT_NAME.pfx"

echo "🔐 Полная настройка HTTPS для Yess Backend"
echo "=========================================="
echo ""

# 1. Создание сертификата
echo "📝 Шаг 1: Создание SSL сертификата..."
if [ -f "$CERT_PATH" ]; then
    echo "⚠️  Сертификат уже существует: $CERT_PATH"
    read -p "Пересоздать? (y/n): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Используется существующий сертификат."
    else
        echo "Пересоздание сертификата..."
        sudo rm -f "$CERT_PATH" "$CERT_DIR/$CERT_NAME.pem" "$KEY_DIR/$CERT_NAME-key.pem"
    fi
fi

if [ ! -f "$CERT_PATH" ]; then
    # Создаём директории
    sudo mkdir -p "$CERT_DIR" "$KEY_DIR"
    
    # Создаём сертификат и ключ
    sudo openssl req -x509 -newkey rsa:4096 \
        -keyout "$KEY_DIR/$CERT_NAME-key.pem" \
        -out "$CERT_DIR/$CERT_NAME.pem" \
        -days 365 -nodes \
        -subj "/CN=$IP_ADDRESS/O=Yess Loyalty/C=KG" \
        -addext "subjectAltName=IP:$IP_ADDRESS" 2>/dev/null
    
    # Преобразуем в PFX формат
    sudo openssl pkcs12 -export \
        -out "$CERT_PATH" \
        -inkey "$KEY_DIR/$CERT_NAME-key.pem" \
        -in "$CERT_DIR/$CERT_NAME.pem" \
        -passout pass:"$PASSWORD" \
        -name "Yess Backend Certificate" 2>/dev/null
    
    # Устанавливаем права доступа
    sudo chmod 644 "$CERT_PATH"
    sudo chmod 600 "$KEY_DIR/$CERT_NAME-key.pem"
    
    echo "✅ Сертификат создан: $CERT_PATH"
else
    echo "✅ Сертификат уже существует: $CERT_PATH"
fi

# 2. Открытие портов в firewall
echo ""
echo "🔥 Шаг 2: Настройка firewall..."
if command -v ufw &> /dev/null; then
    sudo ufw allow 8443/tcp || true
    sudo ufw allow 8000/tcp || true
    echo "✅ Порты 8000 и 8443 открыты в UFW"
elif command -v firewall-cmd &> /dev/null; then
    sudo firewall-cmd --permanent --add-port=8443/tcp || true
    sudo firewall-cmd --permanent --add-port=8000/tcp || true
    sudo firewall-cmd --reload || true
    echo "✅ Порты 8000 и 8443 открыты в firewalld"
else
    echo "⚠️  Firewall не найден (ufw или firewalld). Откройте порты вручную."
fi

# 3. Обновление systemd service (если существует)
echo ""
echo "🔧 Шаг 3: Обновление systemd service..."
SERVICE_NAME="yess-backend"
SERVICE_FILE="/etc/systemd/system/${SERVICE_NAME}.service"

if [ -f "$SERVICE_FILE" ]; then
    echo "Обновление существующего service файла..."
    
    # Проверяем, есть ли уже переменные окружения
    if grep -q "ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PATH" "$SERVICE_FILE"; then
        echo "⚠️  Service файл уже содержит настройки сертификата"
        echo "Переменные окружения уже настроены"
    else
        echo "Добавление переменных окружения в service файл..."
        
        # Создаём временный файл с обновлёнными переменными
        sudo sed -i '/\[Service\]/a Environment=ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PATH='"$CERT_PATH"'
Environment=ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PASSWORD='"$PASSWORD" "$SERVICE_FILE"
        
        sudo systemctl daemon-reload
        echo "✅ Service файл обновлён"
    fi
    
    # Перезапускаем service
    echo ""
    echo "🔄 Перезапуск service..."
    sudo systemctl restart $SERVICE_NAME
    sleep 2
    
    echo ""
    echo "📊 Статус service:"
    sudo systemctl status $SERVICE_NAME --no-pager -l | head -20
else
    echo "⚠️  Service файл не найден: $SERVICE_FILE"
    echo "Запустите setup_service_quick.sh для создания service файла"
fi

# 4. Проверка портов
echo ""
echo "🔍 Шаг 4: Проверка портов..."
echo ""
if sudo netstat -tlnp 2>/dev/null | grep -q ":5000"; then
    echo "✅ HTTP порт 5000 слушается"
else
    echo "❌ HTTP порт 5000 НЕ слушается"
fi

if sudo netstat -tlnp 2>/dev/null | grep -q ":5001"; then
    echo "✅ HTTPS порт 5001 слушается"
else
    echo "⚠️  HTTPS порт 5001 НЕ слушается (возможно, сертификат не настроен в service)"
fi

# 5. Финальная информация
echo ""
echo "=========================================="
echo "✅ Настройка HTTPS завершена!"
echo ""
echo "📋 Информация:"
echo "   Сертификат: $CERT_PATH"
echo "   Пароль: $PASSWORD"
echo ""
echo "📝 Переменные окружения для systemd service:"
echo "   ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PATH=$CERT_PATH"
echo "   ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PASSWORD=$PASSWORD"
echo ""
echo "🔗 Доступ к API:"
echo "   HTTP:  http://$IP_ADDRESS:8000"
echo "   HTTPS: https://$IP_ADDRESS:8443"
echo "   Swagger: http://$IP_ADDRESS:8000/docs"
echo ""
echo "🧪 Проверка подключения:"
echo "   curl -vk https://$IP_ADDRESS:8443/health"
echo ""

