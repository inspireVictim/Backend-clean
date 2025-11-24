using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер дашборда партнера
/// Соответствует /api/v1/partner из Python API
/// </summary>
[ApiController]
[Route("api/v1/partner")]
[Tags("Partner Dashboard")]
[Authorize]
public class PartnerDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PartnerDashboardController> _logger;

    public PartnerDashboardController(
        ApplicationDbContext context,
        ILogger<PartnerDashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Получить данные текущего партнера
    /// GET /api/v1/partner/me
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> GetCurrentPartner()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "Неверный токен" });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == userId.Value);

            if (user == null)
            {
                return Unauthorized(new { error = "Пользователь не найден" });
            }

            // Проверяем, является ли пользователь партнером
            var partnerEmployee = await _context.PartnerEmployees
                .FirstOrDefaultAsync(pe => pe.UserId == userId.Value);

            Domain.Entities.Partner? partner = null;
            if (partnerEmployee != null)
            {
                partner = await _context.Partners
                    .FirstOrDefaultAsync(p => p.Id == partnerEmployee.PartnerId);
            }
            else
            {
                // Проверяем, является ли пользователь владельцем партнера
                partner = await _context.Partners
                    .FirstOrDefaultAsync(p => p.OwnerId == userId.Value);
            }

            if (partner == null)
            {
                return Forbid("Пользователь не является партнером");
            }

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                username = user.FirstName ?? "Partner",
                name = (!string.IsNullOrWhiteSpace($"{user.FirstName ?? ""} {user.LastName ?? ""}".Trim()) ? $"{user.FirstName ?? ""} {user.LastName ?? ""}".Trim() : "Partner"),
                first_name = user.FirstName ?? "",
                last_name = user.LastName ?? "",
                phone = user.Phone,
                role = "partner",
                avatar_url = user.AvatarUrl,
                partner_id = partner.Id,
                partner_name = partner.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения данных партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить статистику партнера
    /// GET /api/v1/partner/stats
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetPartnerStats()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "Неверный токен" });
            }

            // Получаем партнера
            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.OwnerId == userId.Value);

            if (partner == null)
            {
                var partnerEmployee = await _context.PartnerEmployees
                    .FirstOrDefaultAsync(pe => pe.UserId == userId.Value);
                
                if (partnerEmployee != null)
                {
                    partner = await _context.Partners
                        .FirstOrDefaultAsync(p => p.Id == partnerEmployee.PartnerId);
                }
            }

            if (partner == null)
            {
                return Forbid("Пользователь не является партнером");
            }

            // Подсчитываем статистику
            var totalOrders = await _context.Orders
                .CountAsync(o => o.PartnerId == partner.Id);

            var totalTransactions = await _context.Transactions
                .CountAsync(t => t.PartnerId == partner.Id);

            var totalRevenue = await _context.Transactions
                .Where(t => t.PartnerId == partner.Id && t.Status == "completed")
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            return Ok(new
            {
                partner_id = partner.Id,
                total_orders = totalOrders,
                total_transactions = totalTransactions,
                total_revenue = totalRevenue,
                partner_name = partner.Name
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения статистики партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить статистику дашборда партнера
    /// GET /api/v1/partner/dashboard/stats
    /// </summary>
    [HttpGet("dashboard/stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetDashboardStats()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "Неверный токен" });
            }

            // Получаем партнера
            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.OwnerId == userId.Value);

            if (partner == null)
            {
                var partnerEmployee = await _context.PartnerEmployees
                    .FirstOrDefaultAsync(pe => pe.UserId == userId.Value);
                
                if (partnerEmployee != null)
                {
                    partner = await _context.Partners
                        .FirstOrDefaultAsync(p => p.Id == partnerEmployee.PartnerId);
                }
            }

            if (partner == null)
            {
                return Forbid("Пользователь не является партнером");
            }

            // Подсчитываем расширенную статистику
            var totalOrders = await _context.Orders
                .CountAsync(o => o.PartnerId == partner.Id);

            var completedOrders = await _context.Orders
                .CountAsync(o => o.PartnerId == partner.Id && o.Status == Domain.Entities.OrderStatus.Completed);

            var totalTransactions = await _context.Transactions
                .CountAsync(t => t.PartnerId == partner.Id);

            var totalRevenue = await _context.Transactions
                .Where(t => t.PartnerId == partner.Id && t.Status == "completed")
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            var todayRevenue = await _context.Transactions
                .Where(t => t.PartnerId == partner.Id && 
                           t.Status == "completed" && 
                           t.CreatedAt.Date == DateTime.UtcNow.Date)
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            var locationsCount = await _context.PartnerLocations
                .CountAsync(l => l.PartnerId == partner.Id && l.IsActive);

            return Ok(new
            {
                data = new
                {
                    partner_id = partner.Id,
                    partner_name = partner.Name,
                    total_orders = totalOrders,
                    completed_orders = completedOrders,
                    total_transactions = totalTransactions,
                    total_revenue = totalRevenue,
                    today_revenue = todayRevenue,
                    locations_count = locationsCount
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения статистики дашборда партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить транзакции партнера
    /// GET /api/v1/partner/transactions
    /// </summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetPartnerTransactions(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized(new { error = "Неверный токен" });
            }

            // Получаем партнера
            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.OwnerId == userId.Value);

            if (partner == null)
            {
                var partnerEmployee = await _context.PartnerEmployees
                    .FirstOrDefaultAsync(pe => pe.UserId == userId.Value);
                
                if (partnerEmployee != null)
                {
                    partner = await _context.Partners
                        .FirstOrDefaultAsync(p => p.Id == partnerEmployee.PartnerId);
                }
            }

            if (partner == null)
            {
                return Forbid("Пользователь не является партнером");
            }

            var transactions = await _context.Transactions
                .Where(t => t.PartnerId == partner.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            var total = await _context.Transactions
                .CountAsync(t => t.PartnerId == partner.Id);

            return Ok(new
            {
                items = transactions.Select(t => new
                {
                    id = t.Id,
                    user_id = t.UserId,
                    partner_id = t.PartnerId,
                    amount = t.Amount,
                    type = t.Type,
                    status = t.Status,
                    description = t.Description,
                    created_at = t.CreatedAt,
                    completed_at = t.CompletedAt
                }),
                total = total,
                limit = limit,
                offset = offset
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения транзакций партнера");
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
}

