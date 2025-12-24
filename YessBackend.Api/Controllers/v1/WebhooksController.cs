using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YessBackend.Infrastructure.Data;
using System.Text.Json;

namespace YessBackend.Api.Controllers.v1;

[ApiController]
[Route("api/v1/webhooks/payment")]
public class WebhooksController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(ApplicationDbContext context, ILogger<WebhooksController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpPost("callback")]
    public async Task<IActionResult> PaymentCallback([FromBody] JsonElement request)
    {
        // Логируем входящий запрос целиком для отладки
        _logger.LogInformation("RAW JSON RECEIVED: {Json}", request.GetRawText());

        try
        {
            // 1. Извлекаем статус и сумму
            string? status = request.TryGetProperty("status", out var st) ? st.GetString() : null;
            decimal amount = request.TryGetProperty("amount", out var am) ? am.GetDecimal() : 0;

            _logger.LogInformation("Parsed Payment Data: Status={Status}, Amount={Amount}", status, amount);

            // 2. Извлекаем описание (через вложенный объект data)
            string? description = "";
            if (request.TryGetProperty("data", out var data))
            {
                description = data.TryGetProperty("description", out var desc) ? desc.GetString() : "";
            }

            // 3. Поиск UserID в строке описания
            string? userIdStr = null;
            if (!string.IsNullOrEmpty(description) && description.Contains("USER_ID:"))
            {
                // Используем вашу логику разделения строки
                userIdStr = description.Split("USER_ID:")[1].Trim();
            }

            // 4. Проверка условий для начисления
            // Averspay может присылать 'succeeded', 'success' или другие финальные статусы
            if ((status == "succeeded" || status == "success") && !string.IsNullOrEmpty(userIdStr))
            {
                if (int.TryParse(userIdStr, out int targetUserId))
                {
                    _logger.LogInformation("Attempting to update wallet for UserID: {UserId}", targetUserId);

                    // Ищем кошелек пользователя
                    var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == targetUserId);

                    if (wallet != null)
                    {
                        // --- ЛОГИКА МНОЖИТЕЛЕЙ (ПО ТЗ) ---
                        // Если сумма >= 5000, множитель x10, иначе x5
                        decimal multiplier = amount >= 5000m ? 10m : 5m;
                        decimal yescoinBonus = amount * multiplier;
                        // ---------------------------------

                        // Обновляем балансы
                        wallet.Balance += amount;               // Реальные деньги (сомы)
                        wallet.YescoinBalance += yescoinBonus;  // Бонусные коины
                        wallet.TotalEarned += amount;           // Общая статистика
                        wallet.LastUpdated = DateTime.UtcNow;

                        await _context.SaveChangesAsync();

                        _logger.LogInformation("!!! PAYMENT SUCCESS !!! User: {UserId}, Amount: {Am}, Coins Added: {Coins} (x{Mult})",
                            targetUserId, amount, yescoinBonus, multiplier);

                        return Ok(new { status = "success", message = "Wallet updated" });
                    }
                    else
                    {
                        _logger.LogWarning("!!! User {UserId} found but WALLET record is missing in database", targetUserId);
                    }
                }
                else
                {
                    _logger.LogWarning("!!! Could not parse UserId from string: {Raw}", userIdStr);
                }
            }
            else
            {
                _logger.LogInformation("Webhook processed: No action taken (Status: {Status}, UserStr: {UserStr})", status, userIdStr);
            }

            return Ok(new { status = "processed_with_no_action" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FATAL ERROR in PaymentCallback: {Message}", ex.Message);
            // Возвращаем 200, чтобы платежка не долбила сервер повторно при ошибках кода, 
            // но логируем всё для ручного разбора
            return Ok(new { status = "error", message = "Internal error occurred" });
        }
    }
}