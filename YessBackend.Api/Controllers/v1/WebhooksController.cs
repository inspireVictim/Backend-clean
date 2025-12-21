using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YessBackend.Infrastructure.Data;
using YessBackend.Domain.Entities;

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
    public async Task<IActionResult> PaymentCallback([FromBody] WebhookRequest request)
    {
        _logger.LogInformation("Webhook: Пополнение баланса. ID Транзакции: {Id}, Статус: {Status}", request.OrderId, request.Status);

        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == request.OrderId);

        if (transaction == null)
        {
            _logger.LogError("Webhook: Транзакция {Id} не найдена", request.OrderId);
            return NotFound(new { error = "Transaction not found" });
        }

        if (transaction.Status == "SUCCESS" || transaction.Status == "completed")
        {
            return Ok(new { status = "already_processed" });
        }

        if (request.Status == "SUCCEEDED" || request.Status == "success" || request.Status == "Paid")
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == transaction.UserId);
            
            if (wallet != null)
            {
                transaction.BalanceBefore = wallet.YescoinBalance;

                // Начисляем коины (1 к 1 к сумме пополнения)
                wallet.YescoinBalance += transaction.Amount; 
                wallet.TotalEarned += transaction.Amount;
                wallet.LastUpdated = DateTime.UtcNow;

                transaction.BalanceAfter = wallet.YescoinBalance;
                transaction.Status = "SUCCESS";
                transaction.CompletedAt = DateTime.UtcNow;
                transaction.ProcessedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                
                _logger.LogInformation("Webhook: Баланс обновлен. User: {UserId}, +{Amount} коинов", 
                    transaction.UserId, transaction.Amount);
            }
            else
            {
                return NotFound(new { error = "Wallet not found" });
            }
        }
        else 
        {
            transaction.Status = "FAILED";
            await _context.SaveChangesAsync();
        }

        return Ok(new { status = "success" });
    }
}

public class WebhookRequest
{
    public int OrderId { get; set; }
    public string Status { get; set; }
}
