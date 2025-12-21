using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YessBackend.Infrastructure.Data;
using YessBackend.Domain.Entities;
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
        string? gatewayId = request.TryGetProperty("transactionId", out var gId) ? gId.GetString() : "test";
        string? status = request.TryGetProperty("status", out var st) ? st.GetString() : null;
        decimal amount = request.TryGetProperty("amount", out var am) ? am.GetDecimal() : 0;

        _logger.LogInformation("Webhook TEST MODE: Amount {Amount}, Status {Status}", amount, status);

        if (status == "succeeded" || status == "success")
        {
            // Прямое начисление пользователю 13 для теста коэффициентов
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == 13);
            
            if (wallet != null)
            {
                decimal multiplier = amount >= 5000 ? 5 : amount >= 4000 ? 4 : amount >= 3000 ? 3 : amount >= 500 ? 2 : 1;
                decimal yescoinBonus = amount * multiplier;

                wallet.YescoinBalance += yescoinBonus;
                wallet.Balance += amount;
                wallet.TotalEarned += amount;
                wallet.LastUpdated = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("!!! TEST SUCCESS !!! User 13 пополнен на {Amount} сом. Начислено {Bonus} коинов (x{M})", amount, yescoinBonus, multiplier);
                return Ok(new { status = "success", debug = "forced_to_user_13" });
            }
        }

        return Ok(new { status = "ignored" });
    }
}
