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
        _logger.LogInformation("RAW JSON: {Json}", request.GetRawText());

        string? status = request.TryGetProperty("status", out var st) ? st.GetString() : null;
        decimal amount = request.TryGetProperty("amount", out var am) ? am.GetDecimal() : 0;
        
        // Пытаемся вытащить ID из описания (Data -> name_en или description)
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

        if ((status == "succeeded" || status == "success") && !string.IsNullOrEmpty(userIdStr))
        {
            if (int.TryParse(userIdStr, out int targetUserId))
            {
                var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == targetUserId);
                if (wallet != null)
                {
                    wallet.YescoinBalance += amount; // Начислить 1 к 1 для теста
                    wallet.Balance += amount;
                    await _context.SaveChangesAsync();
                    _logger.LogInformation("!!! MATCH SUCCESS !!! User {UserId} found via description", targetUserId);
                    return Ok(new { status = "success" });
                }
            }
        }
        return Ok(new { status = "user_not_found" });
    }
}
