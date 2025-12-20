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
                _logger.LogInformation("REAL TOP-UP: Updating wallet for User 13. Amount: {Amount}", amount);

                using (var transaction = await _context.Database.BeginTransactionAsync())
                {
                    try
                    {
                        // 1. Обновляем кошелек: Balance, YescoinBalance и TotalEarned
                        int affected = await _context.Database.ExecuteSqlRawAsync(
                            @"UPDATE wallets 
                              SET ""Balance"" = ""Balance"" + {0}, 
                                  ""YescoinBalance"" = ""YescoinBalance"" + {0}, 
                                  ""TotalEarned"" = ""TotalEarned"" + {0},
                                  ""LastUpdated"" = {1} 
                              WHERE ""UserId"" = 13",
                            amount, DateTime.UtcNow);

                        _logger.LogInformation("Rows affected in wallets: {Count}", affected);

                        // 2. Создаем запись в транзакциях с заполнением всех NOT NULL колонок
                        // Добавляем Commission, YescoinUsed и YescoinEarned со значениями 0
                        await _context.Database.ExecuteSqlRawAsync(
                            @"INSERT INTO transactions (
                                ""UserId"", ""Type"", ""Amount"", ""Status"", ""Description"", 
                                ""CreatedAt"", ""Commission"", ""YescoinUsed"", ""YescoinEarned""
                              )
                              VALUES (13, 'TOPUP', {0}, 'SUCCESS', 'Finik Top-up', {1}, 0, 0, 0)",
                            amount, DateTime.UtcNow);

                        await transaction.CommitAsync();
                        _logger.LogInformation("SUCCESS: Wallet 13 updated and transaction recorded.");
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        _logger.LogError(ex, "Update failed for user 13 during transaction recording");
                        throw; // Пробрасываем, чтобы увидеть в логах полный стек
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
