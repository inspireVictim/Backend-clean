using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер аналитики
/// Соответствует /api/v1/admin/analytics и /api/v1/partner/analytics из Python API
/// </summary>
[ApiController]
[Route("api/v1")]
[Tags("Analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        ApplicationDbContext context,
        ILogger<AnalyticsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Получить аналитику для админ-панели
    /// GET /api/v1/admin/analytics/dashboard
    /// </summary>
    [HttpGet("admin/analytics/dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetAdminAnalytics(
        [FromQuery] DateTime? start_date = null,
        [FromQuery] DateTime? end_date = null)
    {
        try
        {
            var query = _context.Transactions
                .Where(t => t.Status == "completed");

            if (start_date.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= start_date.Value);
            }

            if (end_date.HasValue)
            {
                query = query.Where(t => t.CreatedAt <= end_date.Value);
            }

            // Средний чек
            var totalRevenue = await query.SumAsync(t => (decimal?)t.Amount) ?? 0;
            var transactionCount = await query.CountAsync();
            var averageOrder = transactionCount > 0 ? totalRevenue / transactionCount : 0;

            // Конверсия (завершенные транзакции / все транзакции)
            var totalTransactions = await _context.Transactions.CountAsync();
            var conversionRate = totalTransactions > 0 
                ? (double)transactionCount / totalTransactions * 100 
                : 0;

            // Распределение пользователей по городам
            var usersByCity = await _context.Users
                .Where(u => u.CityId.HasValue)
                .GroupBy(u => u.CityId)
                .Select(g => new
                {
                    city_id = g.Key,
                    count = g.Count()
                })
                .Join(_context.Cities,
                    u => u.city_id,
                    c => c.Id,
                    (u, c) => new { name = c.Name, value = u.count })
                .ToListAsync();

            // Типы транзакций
            var transactionTypes = await query
                .GroupBy(t => t.Type)
                .Select(g => new
                {
                    name = g.Key ?? "unknown",
                    value = g.Count()
                })
                .ToListAsync();

            // Тренд выручки по времени (последние 30 дней)
            var revenueTrend = await query
                .Where(t => t.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new
                {
                    date = g.Key.ToString("dd.MM"),
                    revenue = g.Sum(t => (decimal?)t.Amount) ?? 0,
                    transactions = g.Count()
                })
                .OrderBy(r => r.date)
                .ToListAsync();

            // Топ партнеров по заказам и выручке
            var partnerPerformance = await query
                .Where(t => t.PartnerId.HasValue)
                .GroupBy(t => t.PartnerId)
                .Select(g => new
                {
                    partner_id = g.Key,
                    orders = g.Count(),
                    revenue = g.Sum(t => (decimal?)t.Amount) ?? 0
                })
                .Join(_context.Partners,
                    p => p.partner_id,
                    partner => partner.Id,
                    (p, partner) => new
                    {
                        name = partner.Name,
                        orders = p.orders,
                        revenue = p.revenue
                    })
                .OrderByDescending(p => p.orders)
                .Take(10)
                .ToListAsync();

            return Ok(new
            {
                data = new
                {
                    average_order = averageOrder,
                    conversion = conversionRate,
                    users_by_city = usersByCity,
                    transaction_types = transactionTypes,
                    revenue_trend = revenueTrend,
                    partner_performance = partnerPerformance
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения аналитики админа");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить аналитику для партнера
    /// GET /api/v1/partner/analytics/dashboard
    /// </summary>
    [HttpGet("partner/analytics/dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetPartnerAnalytics(
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

            // Статистика по заказам
            var totalOrders = await _context.Orders
                .CountAsync(o => o.PartnerId == partner.Id);
            var completedOrders = await _context.Orders
                .CountAsync(o => o.PartnerId == partner.Id && o.Status == Domain.Entities.OrderStatus.Completed);

            // Выручка по периодам
            var totalRevenue = await query.SumAsync(t => (decimal?)t.Amount) ?? 0;
            var revenueByPeriod = await query
                .GroupBy(t => t.CreatedAt.Date)
                .Select(g => new
                {
                    date = g.Key.ToString("yyyy-MM-dd"),
                    revenue = g.Sum(t => (decimal?)t.Amount) ?? 0
                })
                .OrderBy(r => r.date)
                .ToListAsync();

            // Статистика по сотрудникам
            var employeesCount = await _context.PartnerEmployees
                .CountAsync(pe => pe.PartnerId == partner.Id);

            return Ok(new
            {
                data = new
                {
                    partner_id = partner.Id,
                    total_orders = totalOrders,
                    completed_orders = completedOrders,
                    total_revenue = totalRevenue,
                    revenue_by_period = revenueByPeriod,
                    employees_count = employeesCount
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения аналитики партнера");
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

