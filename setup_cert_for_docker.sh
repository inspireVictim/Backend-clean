#!/bin/bash
# Создание SSL сертификата для Docker
# Использование: ./setup_cert_for_docker.sh

set -e

CERT_DIR="./certs"
CERT_NAME="yess-cert"
PASSWORD="YesSGo!@#!"
IP_ADDRESS="5.59.232.211"
CERT_PATH="$CERT_DIR/$CERT_NAME.pfx"

echo "🔐 Создание SSL сертификата для Docker"
echo "======================================"
echo ""

# Создаём директорию для сертификатов
mkdir -p "$CERT_DIR"

# Проверяем, существует ли сертификат
if [ -f "$CERT_PATH" ]; then
    echo "⚠️  Сертификат уже существует: $CERT_PATH"
    read -p "Пересоздать? (y/n): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Yy]$ ]]; then
        echo "Используется существующий сертификат."
        exit 0
    fi
    rm -f "$CERT_PATH" "$CERT_DIR/$CERT_NAME.pem" "$CERT_DIR/$CERT_NAME-key.pem"
fi

echo "📝 Создание сертификата..."
echo "   IP адрес: $IP_ADDRESS"
echo "   Пароль: $PASSWORD"
echo ""

# Создаём сертификат и ключ
openssl req -x509 -newkey rsa:4096 \
    -keyout "$CERT_DIR/$CERT_NAME-key.pem" \
    -out "$CERT_DIR/$CERT_NAME.pem" \
    -days 365 -nodes \
    -subj "/CN=$IP_ADDRESS/O=Yess Loyalty/C=KG" \
    -addext "subjectAltName=IP:$IP_ADDRESS" 2>/dev/null

# Преобразуем в PFX формат
openssl pkcs12 -export \
    -out "$CERT_PATH" \
    -inkey "$CERT_DIR/$CERT_NAME-key.pem" \
    -in "$CERT_DIR/$CERT_NAME.pem" \
    -passout pass:"$PASSWORD" \
    -name "Yess Backend Certificate" 2>/dev/null

# Устанавливаем права доступа
chmod 644 "$CERT_PATH"
chmod 600 "$CERT_DIR/$CERT_NAME-key.pem"

echo ""
echo "✅ Сертификат успешно создан!"
echo "   📁 Расположение: $CERT_PATH"
echo "   🔑 Пароль: $PASSWORD"
echo ""
echo "📋 Информация о сертификате:"
openssl pkcs12 -in "$CERT_PATH" -nokeys -passin pass:"$PASSWORD" 2>/dev/null | \
    openssl x509 -noout -subject -dates 2>/dev/null || echo "   (не удалось прочитать информацию)"
echo ""
echo "✅ Готово! Теперь запустите docker-compose up -d"
echo ""

