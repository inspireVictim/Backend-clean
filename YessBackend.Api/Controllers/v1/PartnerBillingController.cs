using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер биллинга партнера
/// Соответствует /api/v1/partner/billing из Python API
/// </summary>
[ApiController]
[Route("api/v1/partner/billing")]
[Tags("Partner Billing")]
[Authorize]
public class PartnerBillingController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PartnerBillingController> _logger;

    public PartnerBillingController(
        ApplicationDbContext context,
        ILogger<PartnerBillingController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Получить статистику биллинга партнера
    /// GET /api/v1/partner/billing
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetBilling(
        [FromQuery] DateTime? start_date = null,
        [FromQuery] DateTime? end_date = null)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "Неверный токен" });
            }

            var partner = await GetCurrentPartnerAsync(userId.Value);
            if (partner == null)
            {
                return Forbid("Пользователь не является партнером");
            }

            var query = _context.Transactions
                .Where(t => t.PartnerId == partner.Id && t.Status == "completed");

            if (start_date.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= start_date.Value);
            }

            if (end_date.HasValue)
            {
                query = query.Where(t => t.CreatedAt <= end_date.Value);
            }

            var totalRevenue = await query.SumAsync(t => (decimal?)t.Amount) ?? 0;
            var transactionCount = await query.CountAsync();
            var averageTransaction = transactionCount > 0 ? totalRevenue / transactionCount : 0;

            // Статистика по месяцам
            var monthlyStats = await query
                .GroupBy(t => new { Year = t.CreatedAt.Year, Month = t.CreatedAt.Month })
                .Select(g => new
                {
                    year = g.Key.Year,
                    month = g.Key.Month,
                    revenue = g.Sum(t => (decimal?)t.Amount) ?? 0,
                    count = g.Count()
                })
                .OrderByDescending(s => s.year)
                .ThenByDescending(s => s.month)
                .Take(12)
                .ToListAsync();

            return Ok(new
            {
                data = new
                {
                    partner_id = partner.Id,
                    total_revenue = totalRevenue,
                    transaction_count = transactionCount,
                    average_transaction = averageTransaction,
                    monthly_stats = monthlyStats,
                    period = new
                    {
                        start_date = start_date,
                        end_date = end_date
                    }
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения биллинга партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(userIdClaim, out var userId))
        {
            return userId;
        }

        return null;
    }

    private async Task<Domain.Entities.Partner?> GetCurrentPartnerAsync(int userId)
    {
        var partnerEmployee = await _context.PartnerEmployees
            .FirstOrDefaultAsync(pe => pe.UserId == userId);

        if (partnerEmployee != null)
        {
            return await _context.Partners
                .FirstOrDefaultAsync(p => p.Id == partnerEmployee.PartnerId);
        }

        return await _context.Partners
            .FirstOrDefaultAsync(p => p.OwnerId == userId);
    }
}

