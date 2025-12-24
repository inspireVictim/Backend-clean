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
        _logger.LogInformation("RAW JSON RECEIVED: {Json}", request.GetRawText());

        // 1. Извлекаем статус и сумму
        string? status = request.TryGetProperty("status", out var st) ? st.GetString() : null;
        decimal amount = request.TryGetProperty("amount", out var am) ? am.GetDecimal() : 0;

        // 2. Извлекаем описание для поиска UserID
        string? description = "";
        if (request.TryGetProperty("data", out var data))
        {
            description = data.TryGetProperty("description", out var desc) ? desc.GetString() : "";
        }

        string? userIdStr = null;
        if (!string.IsNullOrEmpty(description) && description.Contains("USER_ID:"))
        {
            userIdStr = description.Split("USER_ID:")[1].Trim();
        }

        // Проверяем статус (Averspay присылает 'succeeded')
        if ((status == "succeeded" || status == "success") && !string.IsNullOrEmpty(userIdStr))
        {
            if (int.TryParse(userIdStr, out int targetUserId))
            {
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == targetUserId);
                if (wallet != null)
                {
                    // --- ЛОГИКА МНОЖИТЕЛЕЙ (БЕРЕМ ИЗ ВАШЕГО ТЗ) ---
                    decimal multiplier = amount >= 5000 ? 10m : 5m;
                    decimal yescoinBonus = amount * multiplier;
                    // ----------------------------------------------

                    wallet.Balance += amount;            // Обычные сомы
                    wallet.YescoinBalance += yescoinBonus; // Коины с множителем
                    wallet.TotalEarned += amount;
                    wallet.LastUpdated = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    _logger.LogInformation("!!! PAYMENT SUCCESS !!! User: {UserId}, Amount: {Am}, Coins Added: {Coins} (x{Mult})",
                        targetUserId, amount, yescoinBonus, multiplier);

                    return Ok(new { status = "success" });
                }
                else
                {
                    _logger.LogWarning("!!! User {UserId} found but WALLET missing in DB", targetUserId);
                }
            }
            else
            {
                _logger.LogWarning("!!! Could not parse UserId from string: {Raw}", userIdStr);
            }
        }

        return Ok(new { status = "processed_with_no_action" });
    }
}