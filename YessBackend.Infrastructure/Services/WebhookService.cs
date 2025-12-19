using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using YessBackend.Application.Services;
using YessBackend.Infrastructure.Data;
using System.Threading.Tasks;
using System;
using System.Data;

namespace YessBackend.Infrastructure.Services;

public class WebhookService : IWebhookService
{
    private readonly ILogger<WebhookService> _logger;
    private readonly ApplicationDbContext _context;

    public WebhookService(ILogger<WebhookService> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task ProcessFinikWebhookAsync(JsonElement payload)
    {
        try 
        {
            string? gatewayId = payload.TryGetProperty("transactionId", out var t) ? t.GetString() : null;
            decimal amount = payload.TryGetProperty("amount", out var a) ? a.GetDecimal() : 0;

            if (!string.IsNullOrEmpty(gatewayId))
            {
                _logger.LogInformation("FORCE TEST: Updating wallet for User 13 with amount {Amount}", amount);

                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // Прямое обновление баланса пользователя 13
                        int affected = await _context.Database.ExecuteSqlRawAsync(
                            "UPDATE wallets SET \"Balance\" = \"Balance\" + {0}, \"LastUpdated\" = {1} WHERE \"UserId\" = 13",
                            amount, DateTime.UtcNow);

                        _logger.LogInformation("Rows affected in wallets: {Count}", affected);

                        await transaction.CommitAsync();
                        _logger.LogInformation("FORCE TEST SUCCESS: Wallet 13 updated.");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Force update failed");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Webhook critical error");
        }
    }
}
