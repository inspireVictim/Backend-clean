# 🔐 Инструкция по настройке HTTPS для Yess Backend

## ✅ Что уже настроено автоматически

1. ✅ **Program.cs** - полностью настроен для работы с HTTPS
2. ✅ **docker-compose.yml** - правильный проброс портов (8000:5000, 8443:5001)
3. ✅ **appsettings.json** - правильная структура конфигурации
4. ✅ **Скрипты** - автоматическое создание сертификата и настройка service

## 📋 Что нужно сделать вручную

### Вариант A: Автоматическая настройка (рекомендуется)

Выполните на сервере один скрипт:

```bash
# Скопируйте скрипт на сервер
scp setup_https_complete.sh user@5.59.232.211:/home/yesgoadm/Backend/

# На сервере выполните
cd ~/Backend
sudo chmod +x setup_https_complete.sh
sudo ./setup_https_complete.sh
```

Скрипт автоматически:
- ✅ Создаст SSL сертификат
- ✅ Откроет порты в firewall
- ✅ Обновит systemd service
- ✅ Перезапустит приложение
- ✅ Проверит работу портов

### Вариант B: Ручная настройка (пошагово)

#### Шаг 1: Создание SSL сертификата

```bash
# На сервере
sudo mkdir -p /etc/ssl/certs /etc/ssl/private

# Создание сертификата
sudo openssl req -x509 -newkey rsa:4096 \
    -keyout /etc/ssl/private/yess-cert-key.pem \
    -out /etc/ssl/certs/yess-cert.pem \
    -days 365 -nodes \
    -subj "/CN=5.59.232.211/O=Yess Loyalty/C=KG" \
    -addext "subjectAltName=IP:5.59.232.211"

# Преобразование в PFX
sudo openssl pkcs12 -export \
    -out /etc/ssl/certs/yess-cert.pfx \
    -inkey /etc/ssl/private/yess-cert-key.pem \
    -in /etc/ssl/certs/yess-cert.pem \
    -passout pass:"YesSGo!@#!" \
    -name "Yess Backend Certificate"

# Установка прав доступа
sudo chmod 644 /etc/ssl/certs/yess-cert.pfx
sudo chmod 600 /etc/ssl/private/yess-cert-key.pem
```

#### Шаг 2: Открытие портов в firewall

```bash
# Для UFW
sudo ufw allow 8443/tcp
sudo ufw allow 8000/tcp
sudo ufw status

# Или для firewalld
sudo firewall-cmd --permanent --add-port=8443/tcp
sudo firewall-cmd --permanent --add-port=8000/tcp
sudo firewall-cmd --reload
```

#### Шаг 3: Настройка systemd service

**Если используете скрипт `setup_service_quick.sh`:**

```bash
sudo chmod +x setup_service_quick.sh
sudo ./setup_service_quick.sh
```

Скрипт автоматически создаст service файл с правильными переменными окружения.

**Если настраиваете вручную:**

Отредактируйте `/etc/systemd/system/yess-backend.service`:

```ini
[Service]
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PATH=/etc/ssl/certs/yess-cert.pfx
Environment=ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PASSWORD=YesSGo!@#!
```

Затем:
```bash
sudo systemctl daemon-reload
sudo systemctl restart yess-backend
```

#### Шаг 4: Настройка Docker (если используете)

Отредактируйте `docker-compose.yml`:

```yaml
environment:
  - ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PATH=/etc/ssl/certs/yess-cert.pfx
  - ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PASSWORD=YesSGo!@#!
volumes:
  - /etc/ssl/certs:/etc/ssl/certs:ro
```

Затем:
```bash
docker-compose down
docker-compose up -d
```

## 🔍 Проверка работы

### 1. Проверка портов

```bash
sudo netstat -tlnp | grep -E '(5000|5001)'
# или
sudo ss -tlnp | grep -E '(5000|5001)'
```

Ожидаемый вывод:
```
tcp  0  0  0.0.0.0:5000  0.0.0.0:*  LISTEN  <pid>/dotnet
tcp  0  0  0.0.0.0:5001  0.0.0.0:*  LISTEN  <pid>/dotnet
```

### 2. Проверка HTTP

```bash
# Локально на сервере
curl http://localhost:5000/health

# Снаружи (через Docker)
curl http://5.59.232.211:8000/health

# Swagger
curl http://5.59.232.211:8000/docs
```

### 3. Проверка HTTPS

```bash
# Локально на сервере
curl -vk https://localhost:5001/health

# Снаружи (через Docker)
curl -vk https://5.59.232.211:8443/health

# Swagger
curl -k https://5.59.232.211:8443/docs
```

### 4. Проверка логов

```bash
# systemd
sudo journalctl -u yess-backend -f

# Docker
docker-compose logs -f csharp-backend
```

Ожидаемые сообщения:
- ✅ "HTTP настроен на порту 5000 для обратного прокси"
- ✅ "HTTPS настроен для Production на порту 5001 с сертификатом..."
- ✅ "HTTPS Redirection и HSTS включены" (если HTTPS работает)

## 🔧 Текущая конфигурация

### Порты

- **HTTP**: 5000 (внутри) → 8000 (снаружи через Docker)
- **HTTPS**: 5001 (внутри) → 8443 (снаружи через Docker)

### Пути к сертификату

- **Путь**: `/etc/ssl/certs/yess-cert.pfx`
- **Пароль**: `YesSGo!@#!`

### Переменные окружения

- `ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PATH=/etc/ssl/certs/yess-cert.pfx`
- `ASPNETCORE_KESTREL__CERTIFICATES__DEFAULT__PASSWORD=YesSGo!@#!`

## ⚠️ Важные моменты

1. **Самоподписанный сертификат** подходит только для тестирования
2. **Для production** рекомендуется использовать Let's Encrypt или сертификат от удостоверяющего центра
3. **Приложение не падает** при отсутствии сертификата - работает только по HTTP
4. **HTTPS redirect включается** только если HTTPS успешно настроен

## 🆘 Решение проблем

### Проблема: Порт 8443 не слушается

**Решение:**
1. Проверьте логи: `sudo journalctl -u yess-backend -n 50`
2. Проверьте наличие сертификата: `ls -la /etc/ssl/certs/yess-cert.pfx`
3. Проверьте переменные окружения: `sudo systemctl show yess-backend | grep Environment`
4. Перезапустите service: `sudo systemctl restart yess-backend`

### Проблема: Ошибка при загрузке сертификата

**Решение:**
1. Проверьте пароль в переменных окружения
2. Проверьте права доступа: `sudo chmod 644 /etc/ssl/certs/yess-cert.pfx`
3. Проверьте формат файла: `file /etc/ssl/certs/yess-cert.pfx` (должно быть PKCS12)
4. Пересоздайте сертификат при необходимости

### Проблема: HTTPS redirect не работает

**Решение:**
1. Убедитесь, что `httpsAvailable = true` (проверьте логи)
2. Убедитесь, что HTTPS endpoint успешно настроен
3. Проверьте, что порт 5001 слушается

## ✅ Быстрая команда для проверки

```bash
# Полная проверка
echo "🔍 Проверка HTTP..."
curl -s http://localhost:5000/health | head -1

echo "🔍 Проверка HTTPS..."
curl -sk https://localhost:5001/health | head -1

echo "🔍 Проверка портов..."
sudo ss -tlnp | grep -E '(5000|5001)'
```

## 📝 Чеклист

- [ ] Сертификат создан: `/etc/ssl/certs/yess-cert.pfx`
- [ ] Права доступа установлены: `644` для сертификата
- [ ] Порты открыты в firewall: `8000` и `8443`
- [ ] Переменные окружения настроены в systemd service или docker-compose
- [ ] Service перезапущен: `sudo systemctl restart yess-backend`
- [ ] HTTP работает: `curl http://localhost:5000/health`
- [ ] HTTPS работает: `curl -vk https://localhost:5001/health`
- [ ] Логи показывают успешную настройку HTTPS

## 🎯 Итог

После выполнения всех шагов:
- ✅ HTTP доступен на порту 8000 (внешний) / 5000 (внутренний)
- ✅ HTTPS доступен на порту 8443 (внешний) / 5001 (внутренний)
- ✅ Swagger доступен на `/docs`
- ✅ Приложение не падает при ошибках сертификата
- ✅ HTTPS redirect работает корректно

