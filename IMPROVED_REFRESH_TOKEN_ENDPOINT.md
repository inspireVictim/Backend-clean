# Улучшенный эндпоинт Refresh Token

## Текущая реализация

Текущий эндпоинт `/api/v1/auth/refresh` уже реализован в `AuthController.cs` и включает:

✅ Валидацию refresh token
✅ Проверку типа токена (должен быть "refresh")
✅ Проверку пользователя (активен, не заблокирован)
✅ Выдачу новых access и refresh токенов
✅ Token rotation (новый refresh token при каждом обновлении)

## Рекомендации по улучшению

### 1. Rate Limiting

Добавьте ограничение частоты запросов refresh для предотвращения атак:

```csharp
[HttpPost("refresh")]
[Consumes("application/json")]
[EnableRateLimiting("RefreshTokenPolicy")] // Добавить rate limiting
public async Task<ActionResult<TokenResponseDto>> Refresh([FromBody] RefreshTokenRequestDto request)
{
    // ... существующий код
}
```

Настройка в `Program.cs`:
```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("RefreshTokenPolicy", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 5; // Максимум 5 запросов в минуту
    });
});
```

### 2. Логирование подозрительной активности

```csharp
// В методе Refresh
if (user == null || !user.IsActive || user.IsBlocked)
{
    _logger.LogWarning(
        "⚠️ [SECURITY] Refresh attempt for invalid user: Phone={Phone}, UserId={UserId}, IsActive={IsActive}, IsBlocked={IsBlocked}",
        phone, user?.Id, user?.IsActive, user?.IsBlocked);
    return Unauthorized(new { error = "Пользователь не найден или заблокирован" });
}
```

### 3. Проверка IP адреса (опционально)

Для дополнительной безопасности можно проверять, что refresh запросы приходят с того же IP:

```csharp
// Получить IP адрес клиента
var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
_logger.LogInformation("Refresh request from IP: {Ip}", clientIp);

// Сохранить IP в токене или БД для проверки при следующем refresh
```

### 4. Отзыв токенов (Token Revocation)

Для возможности отзыва токенов при компрометации:

```csharp
// Проверить, не отозван ли refresh token
var isRevoked = await _tokenRevocationService.IsTokenRevokedAsync(request.RefreshToken);
if (isRevoked)
{
    _logger.LogWarning("⚠️ [SECURITY] Attempt to use revoked refresh token");
    return Unauthorized(new { error = "Токен был отозван" });
}
```

### 5. Улучшенная обработка ошибок

```csharp
catch (SecurityTokenExpiredException)
{
    _logger.LogInformation("Refresh token expired for user");
    return Unauthorized(new { error = "Refresh токен истек. Пожалуйста, войдите заново." });
}
catch (SecurityTokenInvalidSignatureException)
{
    _logger.LogWarning("⚠️ [SECURITY] Invalid refresh token signature");
    return Unauthorized(new { error = "Неверный refresh токен" });
}
catch (SecurityTokenException ex)
{
    _logger.LogWarning(ex, "Security token validation failed");
    return Unauthorized(new { error = "Неверный refresh токен" });
}
```

## Полный улучшенный пример

```csharp
/// <summary>
/// Обновление access/refresh токенов по refresh токену
/// POST /api/v1/auth/refresh
/// </summary>
[HttpPost("refresh")]
[Consumes("application/json")]
[ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
[ProducesResponseType(StatusCodes.Status400BadRequest)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public async Task<ActionResult<TokenResponseDto>> Refresh([FromBody] RefreshTokenRequestDto request)
{
    if (string.IsNullOrWhiteSpace(request.RefreshToken))
    {
        return BadRequest(new { error = "refresh_token is required" });
    }

    try
    {
        var jwtSection = _configuration.GetSection("Jwt");
        var secretKey = jwtSection["SecretKey"];
        if (string.IsNullOrEmpty(secretKey))
        {
            _logger.LogError("JWT SecretKey не настроен в конфигурации");
            throw new InvalidOperationException("JWT SecretKey не настроен");
        }

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(secretKey);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSection["Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero // Без допуска на расхождение времени
        };

        SecurityToken validatedToken;
        ClaimsPrincipal principal;
        
        try
        {
            principal = tokenHandler.ValidateToken(request.RefreshToken, validationParameters, out validatedToken);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.LogInformation("Refresh token expired");
            return Unauthorized(new { error = "Refresh токен истек. Пожалуйста, войдите заново." });
        }
        catch (SecurityTokenInvalidSignatureException ex)
        {
            _logger.LogWarning(ex, "⚠️ [SECURITY] Invalid refresh token signature");
            return Unauthorized(new { error = "Неверный refresh токен" });
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Security token validation failed");
            return Unauthorized(new { error = "Неверный refresh токен" });
        }

        // Проверка типа токена
        var typeClaim = principal.FindFirst("type")?.Value;
        if (!string.Equals(typeClaim, "refresh", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("⚠️ [SECURITY] Attempt to use non-refresh token as refresh token");
            return Unauthorized(new { error = "Invalid token type" });
        }

        // Получение данных пользователя из токена
        var phone = principal.FindFirst("phone")?.Value ?? principal.Identity?.Name;
        var userIdClaim = principal.FindFirst("user_id")?.Value ?? 
                         principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(phone) || string.IsNullOrEmpty(userIdClaim))
        {
            _logger.LogWarning("⚠️ [SECURITY] Refresh token missing required claims");
            return Unauthorized(new { error = "Invalid refresh token" });
        }

        // Проверка пользователя
        var user = await _authService.GetUserByPhoneAsync(phone);
        if (user == null)
        {
            _logger.LogWarning("⚠️ [SECURITY] Refresh attempt for non-existent user: Phone={Phone}", phone);
            return Unauthorized(new { error = "Пользователь не найден" });
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("⚠️ [SECURITY] Refresh attempt for inactive user: UserId={UserId}, Phone={Phone}", 
                user.Id, phone);
            return Unauthorized(new { error = "Пользователь деактивирован" });
        }

        if (user.IsBlocked)
        {
            _logger.LogWarning("⚠️ [SECURITY] Refresh attempt for blocked user: UserId={UserId}, Phone={Phone}", 
                user.Id, phone);
            return Unauthorized(new { error = "Пользователь заблокирован" });
        }

        // Проверка соответствия user_id из токена и БД
        if (!int.TryParse(userIdClaim, out var userIdFromToken) || userIdFromToken != user.Id)
        {
            _logger.LogWarning("⚠️ [SECURITY] User ID mismatch in refresh token: TokenUserId={TokenUserId}, DbUserId={DbUserId}", 
                userIdFromToken, user.Id);
            return Unauthorized(new { error = "Invalid refresh token" });
        }

        // Создание новых токенов (Token Rotation)
        var accessToken = _authService.CreateAccessToken(user);
        var newRefreshToken = _authService.CreateRefreshToken(user);
        var expiresMinutes = jwtSection.GetValue<int>("AccessTokenExpireMinutes", 60);

        _logger.LogInformation("✅ Tokens refreshed successfully for user: UserId={UserId}, Phone={Phone}", 
            user.Id, phone);

        var response = new TokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken, // Новый refresh token (rotation)
            TokenType = "bearer",
            ExpiresIn = expiresMinutes * 60
        };

        return Ok(response);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ Unexpected error during token refresh");
        return StatusCode(500, new { error = "Ошибка сервера при обновлении токена" });
    }
}
```

## Тестирование

### Тест 1: Успешный refresh
```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refresh_token": "valid_refresh_token_here"
}
```

Ожидаемый ответ: 200 OK с новыми токенами

### Тест 2: Истекший refresh token
```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refresh_token": "expired_refresh_token_here"
}
```

Ожидаемый ответ: 401 Unauthorized

### Тест 3: Неверный тип токена (access вместо refresh)
```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refresh_token": "access_token_here"
}
```

Ожидаемый ответ: 401 Unauthorized

### Тест 4: Заблокированный пользователь
```http
POST /api/v1/auth/refresh
Content-Type: application/json

{
  "refresh_token": "refresh_token_of_blocked_user"
}
```

Ожидаемый ответ: 401 Unauthorized

## Безопасность

✅ **Валидация подписи** - проверка SecretKey
✅ **Валидация времени жизни** - проверка exp claim
✅ **Проверка типа токена** - только refresh токены
✅ **Проверка пользователя** - активен, не заблокирован
✅ **Token Rotation** - новый refresh token при каждом обновлении
✅ **Логирование** - все подозрительные действия логируются
✅ **Обработка ошибок** - безопасные сообщения об ошибках

## Дополнительные улучшения (опционально)

1. **Хранение refresh токенов в БД** для возможности отзыва
2. **Device fingerprinting** для привязки токенов к устройству
3. **Геолокация** для обнаружения подозрительной активности
4. **2FA** для дополнительной защиты при refresh

