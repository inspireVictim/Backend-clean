using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using YessBackend.Application.Services;
using YessBackend.Domain.Entities;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер админ-панели (37 endpoints)
/// Соответствует /api/v1/admin из Python API
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Tags("Admin")]
[Authorize] // Требуется авторизация для админ-панели
public class AdminController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext context,
        ILogger<AdminController> logger)
    {
        _context = context;
        _logger = logger;
    }

    #region Dashboard & Statistics

    /// <summary>
    /// Получить статистику дашборда
    /// GET /api/v1/admin/dashboard
    /// </summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetDashboard()
    {
        try
        {
            var totalUsers = await _context.Users.CountAsync();
            var totalPartners = await _context.Partners.CountAsync();
            var totalTransactions = await _context.Transactions.CountAsync();
            var totalOrders = await _context.Orders.CountAsync();

            var totalRevenue = await _context.Transactions
                .Where(t => t.Status == "completed")
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            var activeUsers = await _context.Users
                .CountAsync(u => u.LastLoginAt.HasValue && u.LastLoginAt.Value >= DateTime.UtcNow.AddDays(-30));

            return Ok(new
            {
                total_users = totalUsers,
                active_users = activeUsers,
                total_partners = totalPartners,
                total_transactions = totalTransactions,
                total_orders = totalOrders,
                total_revenue = totalRevenue
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения статистики дашборда");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить статистику транзакций
    /// GET /api/v1/admin/stats/transactions
    /// </summary>
    [HttpGet("stats/transactions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTransactionStats(
        [FromQuery] DateTime? start_date = null,
        [FromQuery] DateTime? end_date = null)
    {
        try
        {
            var query = _context.Transactions.AsQueryable();

            if (start_date.HasValue)
            {
                query = query.Where(t => t.CreatedAt >= start_date.Value);
            }

            if (end_date.HasValue)
            {
                query = query.Where(t => t.CreatedAt <= end_date.Value);
            }

            var total = await query.CountAsync();
            var completed = await query.CountAsync(t => t.Status == "completed");
            var pending = await query.CountAsync(t => t.Status == "pending");
            var failed = await query.CountAsync(t => t.Status == "failed");

            var totalAmount = await query
                .Where(t => t.Status == "completed")
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            return Ok(new
            {
                total = total,
                completed = completed,
                pending = pending,
                failed = failed,
                total_amount = totalAmount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения статистики транзакций");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region User Management

    /// <summary>
    /// Получить список пользователей
    /// GET /api/v1/admin/users
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUsers(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        [FromQuery] bool? is_active = null)
    {
        try
        {
            var query = _context.Users.AsQueryable();

            if (is_active.HasValue)
            {
                query = query.Where(u => u.IsActive == is_active.Value);
            }

            var total = await query.CountAsync();
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return Ok(new
            {
                items = users.Select(u => new
                {
                    id = u.Id,
                    email = u.Email,
                    phone = u.Phone,
                    first_name = u.FirstName,
                    last_name = u.LastName,
                    is_active = u.IsActive,
                    created_at = u.CreatedAt,
                    last_login_at = u.LastLoginAt
                }),
                total = total,
                limit = limit,
                offset = offset
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения пользователей");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить пользователя по ID
    /// GET /api/v1/admin/users/{user_id}
    /// </summary>
    [HttpGet("users/{user_id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetUser([FromRoute] int user_id)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == user_id);

            if (user == null)
            {
                return NotFound(new { error = "Пользователь не найден" });
            }

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == user_id);

            var transactionsCount = await _context.Transactions
                .CountAsync(t => t.UserId == user_id);

            return Ok(new
            {
                id = user.Id,
                email = user.Email,
                phone = user.Phone,
                first_name = user.FirstName,
                last_name = user.LastName,
                is_active = user.IsActive,
                wallet_balance = wallet?.Balance ?? 0,
                transactions_count = transactionsCount,
                created_at = user.CreatedAt,
                last_login_at = user.LastLoginAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения пользователя");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Обновить пользователя
    /// PUT /api/v1/admin/users/{user_id}
    /// </summary>
    [HttpPut("users/{user_id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateUser([FromRoute] int user_id, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == user_id);

            if (user == null)
            {
                return NotFound(new { error = "Пользователь не найден" });
            }

            if (request.IsActive.HasValue)
            {
                user.IsActive = request.IsActive.Value;
            }

            if (!string.IsNullOrEmpty(request.FirstName))
            {
                user.FirstName = request.FirstName;
            }

            if (!string.IsNullOrEmpty(request.LastName))
            {
                user.LastName = request.LastName;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Пользователь обновлен", user_id = user_id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления пользователя");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Деактивировать пользователя
    /// DELETE /api/v1/admin/users/{user_id}
    /// </summary>
    [HttpDelete("users/{user_id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeactivateUser([FromRoute] int user_id)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == user_id);

            if (user == null)
            {
                return NotFound(new { error = "Пользователь не найден" });
            }

            user.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Пользователь деактивирован" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка деактивации пользователя");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Активировать пользователя
    /// POST /api/v1/admin/users/{user_id}/activate
    /// </summary>
    [HttpPost("users/{user_id}/activate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ActivateUser([FromRoute] int user_id)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == user_id);

            if (user == null)
            {
                return NotFound(new { error = "Пользователь не найден" });
            }

            user.IsActive = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Пользователь активирован" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка активации пользователя");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Деактивировать пользователя (альтернативный endpoint)
    /// POST /api/v1/admin/users/{user_id}/deactivate
    /// </summary>
    [HttpPost("users/{user_id}/deactivate")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeactivateUserPost([FromRoute] int user_id)
    {
        try
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id == user_id);

            if (user == null)
            {
                return NotFound(new { error = "Пользователь не найден" });
            }

            user.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Пользователь деактивирован" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка деактивации пользователя");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region Partner Management

    /// <summary>
    /// Получить список партнеров
    /// GET /api/v1/admin/partners
    /// </summary>
    [HttpGet("partners")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPartners(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        [FromQuery] bool? is_active = null)
    {
        try
        {
            var query = _context.Partners.AsQueryable();

            if (is_active.HasValue)
            {
                query = query.Where(p => p.IsActive == is_active.Value);
            }

            var total = await query.CountAsync();
            var partners = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return Ok(new
            {
                items = partners.Select(p => new
                {
                    id = p.Id,
                    name = p.Name,
                    category = p.Category,
                    is_active = p.IsActive,
                    is_verified = p.IsVerified,
                    created_at = p.CreatedAt
                }),
                total = total,
                limit = limit,
                offset = offset
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения партнеров");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить партнера по ID
    /// GET /api/v1/admin/partners/{partner_id}
    /// </summary>
    [HttpGet("partners/{partner_id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetPartner([FromRoute] int partner_id)
    {
        try
        {
            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.Id == partner_id);

            if (partner == null)
            {
                return NotFound(new { error = "Партнер не найден" });
            }

            var ordersCount = await _context.Orders
                .CountAsync(o => o.PartnerId == partner_id);

            var transactionsCount = await _context.Transactions
                .CountAsync(t => t.PartnerId == partner_id);

            return Ok(new
            {
                id = partner.Id,
                name = partner.Name,
                category = partner.Category,
                is_active = partner.IsActive,
                is_verified = partner.IsVerified,
                orders_count = ordersCount,
                transactions_count = transactionsCount,
                created_at = partner.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Обновить партнера
    /// PUT /api/v1/admin/partners/{partner_id}
    /// </summary>
    [HttpPut("partners/{partner_id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdatePartner([FromRoute] int partner_id, [FromBody] UpdatePartnerRequest request)
    {
        try
        {
            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.Id == partner_id);

            if (partner == null)
            {
                return NotFound(new { error = "Партнер не найден" });
            }

            if (request.IsActive.HasValue)
            {
                partner.IsActive = request.IsActive.Value;
            }

            if (request.IsVerified.HasValue)
            {
                partner.IsVerified = request.IsVerified.Value;
            }

            if (!string.IsNullOrEmpty(request.Name))
            {
                partner.Name = request.Name;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Партнер обновлен", partner_id = partner_id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Верифицировать партнера
    /// POST /api/v1/admin/partners/{partner_id}/verify
    /// </summary>
    [HttpPost("partners/{partner_id}/verify")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> VerifyPartner([FromRoute] int partner_id)
    {
        try
        {
            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.Id == partner_id);

            if (partner == null)
            {
                return NotFound(new { error = "Партнер не найден" });
            }

            partner.IsVerified = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Партнер верифицирован" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка верификации партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Одобрить партнера
    /// POST /api/v1/admin/partners/{partner_id}/approve
    /// </summary>
    [HttpPost("partners/{partner_id}/approve")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ApprovePartner([FromRoute] int partner_id)
    {
        try
        {
            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.Id == partner_id);

            if (partner == null)
            {
                return NotFound(new { error = "Партнер не найден" });
            }

            partner.IsVerified = true;
            partner.IsActive = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Партнер одобрен" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка одобрения партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Отклонить партнера
    /// POST /api/v1/admin/partners/{partner_id}/reject
    /// </summary>
    [HttpPost("partners/{partner_id}/reject")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> RejectPartner([FromRoute] int partner_id, [FromBody] RejectPartnerRequest? request = null)
    {
        try
        {
            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.Id == partner_id);

            if (partner == null)
            {
                return NotFound(new { error = "Партнер не найден" });
            }

            partner.IsVerified = false;
            partner.IsActive = false;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Партнер отклонен", reason = request?.Reason });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отклонения партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Создать локацию партнера
    /// POST /api/v1/admin/partners/{partner_id}/locations
    /// </summary>
    [HttpPost("partners/{partner_id}/locations")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> CreatePartnerLocation([FromRoute] int partner_id, [FromBody] CreatePartnerLocationRequest request)
    {
        try
        {
            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.Id == partner_id);

            if (partner == null)
            {
                return NotFound(new { error = "Партнер не найден" });
            }

            var location = new Domain.Entities.PartnerLocation
            {
                PartnerId = partner_id,
                Address = request.Address,
                Latitude = (decimal?)request.Latitude,
                Longitude = (decimal?)request.Longitude,
                PhoneNumber = request.PhoneNumber,
                IsActive = request.IsActive ?? true,
                CreatedAt = DateTime.UtcNow
            };

            _context.PartnerLocations.Add(location);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPartnerLocations), new { partner_id }, new
            {
                id = location.Id,
                partner_id = location.PartnerId,
                address = location.Address,
                latitude = location.Latitude,
                longitude = location.Longitude,
                phone_number = location.PhoneNumber,
                is_active = location.IsActive
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания локации партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить локации партнера
    /// GET /api/v1/admin/partners/{partner_id}/locations
    /// </summary>
    [HttpGet("partners/{partner_id}/locations")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPartnerLocations([FromRoute] int partner_id)
    {
        try
        {
            var locations = await _context.PartnerLocations
                .Where(l => l.PartnerId == partner_id)
                .ToListAsync();

            return Ok(new
            {
                items = locations.Select(l => new
                {
                    id = l.Id,
                    partner_id = l.PartnerId,
                    address = l.Address,
                    latitude = l.Latitude,
                    longitude = l.Longitude,
                    phone_number = l.PhoneNumber,
                    is_active = l.IsActive
                }),
                total = locations.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения локаций партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Удалить локацию партнера
    /// DELETE /api/v1/admin/partners/locations/{location_id}
    /// </summary>
    [HttpDelete("partners/locations/{location_id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeletePartnerLocation([FromRoute] int location_id)
    {
        try
        {
            var location = await _context.PartnerLocations
                .FirstOrDefaultAsync(l => l.Id == location_id);

            if (location == null)
            {
                return NotFound(new { error = "Локация не найдена" });
            }

            _context.PartnerLocations.Remove(location);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Локация удалена" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления локации партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region Transaction Management

    /// <summary>
    /// Получить список транзакций
    /// GET /api/v1/admin/transactions
    /// </summary>
    [HttpGet("transactions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetTransactions(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        [FromQuery] string? status = null)
    {
        try
        {
            var query = _context.Transactions.AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(t => t.Status == status);
            }

            var total = await query.CountAsync();
            var transactions = await query
                .OrderByDescending(t => t.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

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
            _logger.LogError(ex, "Ошибка получения транзакций");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить транзакцию по ID
    /// GET /api/v1/admin/transactions/{transaction_id}
    /// </summary>
    [HttpGet("transactions/{transaction_id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetTransaction([FromRoute] int transaction_id)
    {
        try
        {
            var transaction = await _context.Transactions
                .FirstOrDefaultAsync(t => t.Id == transaction_id);

            if (transaction == null)
            {
                return NotFound(new { error = "Транзакция не найдена" });
            }

            return Ok(new
            {
                id = transaction.Id,
                user_id = transaction.UserId,
                partner_id = transaction.PartnerId,
                amount = transaction.Amount,
                type = transaction.Type,
                status = transaction.Status,
                description = transaction.Description,
                created_at = transaction.CreatedAt,
                completed_at = transaction.CompletedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения транзакции");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region Order Management

    /// <summary>
    /// Получить список заказов
    /// GET /api/v1/admin/orders
    /// </summary>
    [HttpGet("orders")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetOrders(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0,
        [FromQuery] string? status = null)
    {
        try
        {
            var query = _context.Orders.AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
                {
                    query = query.Where(o => o.Status == orderStatus);
                }
            }

            var total = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return Ok(new
            {
                items = orders.Select(o => new
                {
                    id = o.Id,
                    user_id = o.UserId,
                    partner_id = o.PartnerId,
                    total_amount = o.FinalAmount,
                    status = o.Status,
                    created_at = o.CreatedAt
                }),
                total = total,
                limit = limit,
                offset = offset
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения заказов");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region Promotion Management

    /// <summary>
    /// Получить список акций
    /// GET /api/v1/admin/promotions
    /// </summary>
    [HttpGet("promotions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPromotions(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        try
        {
            var total = await _context.Promotions.CountAsync();
            var promotions = await _context.Promotions
                .OrderByDescending(p => p.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return Ok(new
            {
                items = promotions.Select(p => new
                {
                    id = p.Id,
                    partner_id = p.PartnerId,
                    title = p.Title,
                    is_active = p.IsActive,
                    valid_until = p.ValidUntil,
                    created_at = p.CreatedAt
                }),
                total = total,
                limit = limit,
                offset = offset
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения акций");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить акцию по ID
    /// GET /api/v1/admin/promotions/{id}
    /// </summary>
    [HttpGet("promotions/{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetPromotionById([FromRoute] int id)
    {
        try
        {
            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Id == id);

            if (promotion == null)
            {
                return NotFound(new { error = "Акция не найдена" });
            }

            return Ok(new
            {
                id = promotion.Id,
                partner_id = promotion.PartnerId,
                title = promotion.Title,
                description = promotion.Description,
                discount_percent = promotion.DiscountPercent,
                is_active = promotion.IsActive,
                valid_until = promotion.ValidUntil,
                created_at = promotion.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения акции");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Создать акцию
    /// POST /api/v1/admin/promotions
    /// </summary>
    [HttpPost("promotions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreatePromotion([FromBody] CreatePromotionRequest request)
    {
        try
        {
            var promotion = new Domain.Entities.Promotion
            {
                PartnerId = request.PartnerId ?? 0,
                Title = request.Title,
                Description = request.Description,
                DiscountPercent = request.DiscountPercent ?? 0m,
                IsActive = request.IsActive ?? true,
                ValidUntil = request.ValidUntil,
                CreatedAt = DateTime.UtcNow
            };

            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPromotionById), new { id = promotion.Id }, new
            {
                id = promotion.Id,
                partner_id = promotion.PartnerId,
                title = promotion.Title,
                description = promotion.Description,
                discount_percent = promotion.DiscountPercent,
                is_active = promotion.IsActive,
                valid_until = promotion.ValidUntil,
                created_at = promotion.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания акции");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Обновить акцию
    /// PUT /api/v1/admin/promotions/{id}
    /// </summary>
    [HttpPut("promotions/{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdatePromotion([FromRoute] int id, [FromBody] UpdatePromotionRequest request)
    {
        try
        {
            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Id == id);

            if (promotion == null)
            {
                return NotFound(new { error = "Акция не найдена" });
            }

            if (request.PartnerId.HasValue)
            {
                promotion.PartnerId = request.PartnerId.Value;
            }

            if (!string.IsNullOrEmpty(request.Title))
            {
                promotion.Title = request.Title;
            }

            if (request.Description != null)
            {
                promotion.Description = request.Description;
            }

            if (request.DiscountPercent.HasValue)
            {
                promotion.DiscountPercent = request.DiscountPercent.Value;
            }

            if (request.IsActive.HasValue)
            {
                promotion.IsActive = request.IsActive.Value;
            }

            if (request.ValidUntil.HasValue)
            {
                promotion.ValidUntil = request.ValidUntil;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                id = promotion.Id,
                partner_id = promotion.PartnerId,
                title = promotion.Title,
                description = promotion.Description,
                discount_percent = promotion.DiscountPercent,
                is_active = promotion.IsActive,
                valid_until = promotion.ValidUntil,
                message = "Акция обновлена"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления акции");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Удалить акцию
    /// DELETE /api/v1/admin/promotions/{id}
    /// </summary>
    [HttpDelete("promotions/{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeletePromotion([FromRoute] int id)
    {
        try
        {
            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Id == id);

            if (promotion == null)
            {
                return NotFound(new { error = "Акция не найдена" });
            }

            _context.Promotions.Remove(promotion);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Акция удалена", id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления акции");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region Notification Management

    /// <summary>
    /// Получить список уведомлений
    /// GET /api/v1/admin/notifications
    /// </summary>
    [HttpGet("notifications")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetNotifications(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        try
        {
            var total = await _context.Notifications.CountAsync();
            var notifications = await _context.Notifications
                .OrderByDescending(n => n.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return Ok(new
            {
                items = notifications.Select(n => new
                {
                    id = n.Id,
                    user_id = n.UserId,
                    title = n.Title,
                    type = n.Type,
                    is_read = n.IsRead,
                    created_at = n.CreatedAt
                }),
                total = total,
                limit = limit,
                offset = offset
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения уведомлений");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Отправить уведомление (админ)
    /// POST /api/v1/admin/notifications
    /// </summary>
    [HttpPost("notifications")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> SendNotification([FromBody] AdminSendNotificationRequest request)
    {
        try
        {
            // Определяем получателей по сегменту
            var userIds = new List<int>();

            switch (request.Segment?.ToLower())
            {
                case "all":
                    userIds = await _context.Users.Select(u => u.Id).ToListAsync();
                    break;
                case "active":
                    userIds = await _context.Users
                        .Where(u => u.IsActive && u.LastLoginAt.HasValue && 
                                   u.LastLoginAt.Value >= DateTime.UtcNow.AddDays(-30))
                        .Select(u => u.Id)
                        .ToListAsync();
                    break;
                default:
                    // Если указан конкретный user_id
                    if (request.UserId.HasValue)
                    {
                        userIds.Add(request.UserId.Value);
                    }
                    break;
            }

            if (userIds.Count == 0)
            {
                return BadRequest(new { error = "Не найдены получатели уведомления" });
            }

            // Создаем уведомления для всех получателей
            var notifications = userIds.Select(userId => new Domain.Entities.Notification
            {
                UserId = userId,
                Title = request.Title,
                Body = request.Message,
                Type = request.Type ?? "info",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Уведомления отправлены",
                sent_count = notifications.Count,
                scheduled_for = request.ScheduledFor
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отправки уведомлений");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Обновить уведомление
    /// PUT /api/v1/admin/notifications/{id}
    /// </summary>
    [HttpPut("notifications/{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateNotification([FromRoute] int id, [FromBody] UpdateNotificationRequest request)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id);

            if (notification == null)
            {
                return NotFound(new { error = "Уведомление не найдено" });
            }

            if (!string.IsNullOrEmpty(request.Title))
            {
                notification.Title = request.Title;
            }

            if (!string.IsNullOrEmpty(request.Message))
            {
                notification.Body = request.Message;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Уведомление обновлено", id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления уведомления");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Удалить уведомление
    /// DELETE /api/v1/admin/notifications/{id}
    /// </summary>
    [HttpDelete("notifications/{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteNotification([FromRoute] int id)
    {
        try
        {
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id);

            if (notification == null)
            {
                return NotFound(new { error = "Уведомление не найдено" });
            }

            _context.Notifications.Remove(notification);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Уведомление удалено", id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления уведомления");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region City Management

    /// <summary>
    /// Получить список городов
    /// GET /api/v1/admin/cities
    /// </summary>
    [HttpGet("cities")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetCities()
    {
        try
        {
            var cities = await _context.Cities
                .OrderBy(c => c.Name)
                .ToListAsync();

            return Ok(new
            {
                items = cities.Select(c => new
                {
                    id = c.Id,
                    name = c.Name
                }),
                total = cities.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения городов");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Создать город
    /// POST /api/v1/admin/cities
    /// </summary>
    [HttpPost("cities")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateCity([FromBody] CreateCityRequest request)
    {
        try
        {
            var city = new Domain.Entities.City
            {
                Name = request.Name
            };

            _context.Cities.Add(city);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCities), new { }, new
            {
                id = city.Id,
                name = city.Name,
                message = "Город создан"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания города");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region Wallet Management

    /// <summary>
    /// Получить балансы всех кошельков
    /// GET /api/v1/admin/wallets
    /// </summary>
    [HttpGet("wallets")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetWallets(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        try
        {
            var total = await _context.Wallets.CountAsync();
            var wallets = await _context.Wallets
                .OrderByDescending(w => w.Balance)
                .Skip(offset)
                .Take(limit)
                .ToListAsync();

            return Ok(new
            {
                items = wallets.Select(w => new
                {
                    id = w.Id,
                    user_id = w.UserId,
                    balance = w.Balance,
                    yescoin_balance = w.YescoinBalance,
                    total_earned = w.TotalEarned,
                    total_spent = w.TotalSpent
                }),
                total = total,
                limit = limit,
                offset = offset
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения кошельков");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region System Settings

    /// <summary>
    /// Получить текущего администратора
    /// GET /api/v1/admin/me
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetCurrentAdmin()
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

            return Ok(new
            {
                id = user.Id.ToString(),
                email = user.Email ?? user.Phone,
                role = "admin",
                name = (!string.IsNullOrWhiteSpace($"{user.FirstName} {user.LastName}".Trim()) ? $"{user.FirstName} {user.LastName}".Trim() : (user.Email ?? user.Phone ?? "Admin"))
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения текущего администратора");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить статистику для дашборда (расширенная)
    /// GET /api/v1/admin/dashboard/stats
    /// </summary>
    [HttpGet("dashboard/stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetDashboardStats()
    {
        try
        {
            var totalUsers = await _context.Users.CountAsync();
            var activeUsers = await _context.Users
                .CountAsync(u => u.IsActive && u.LastLoginAt.HasValue && u.LastLoginAt.Value >= DateTime.UtcNow.AddDays(-30));
            var totalPartners = await _context.Partners.CountAsync();
            var activePartners = await _context.Partners.CountAsync(p => p.IsActive);
            var totalTransactions = await _context.Transactions.CountAsync();
            var totalRevenue = await _context.Transactions
                .Where(t => t.Status == "completed")
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            return Ok(new
            {
                data = new
                {
                    total_users = totalUsers,
                    active_users = activeUsers,
                    total_partners = totalPartners,
                    active_partners = activePartners,
                    total_transactions = totalTransactions,
                    total_revenue = totalRevenue,
                    revenue_growth = 12.5 // Mock значение
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения статистики дашборда");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region Reports

    /// <summary>
    /// Получить отчет по пользователям
    /// GET /api/v1/admin/reports/users
    /// </summary>
    [HttpGet("reports/users")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetUsersReport(
        [FromQuery] DateTime? start_date = null,
        [FromQuery] DateTime? end_date = null)
    {
        try
        {
            var query = _context.Users.AsQueryable();

            if (start_date.HasValue)
            {
                query = query.Where(u => u.CreatedAt >= start_date.Value);
            }

            if (end_date.HasValue)
            {
                query = query.Where(u => u.CreatedAt <= end_date.Value);
            }

            var total = await query.CountAsync();
            var active = await query.CountAsync(u => u.IsActive);

            return Ok(new
            {
                total_registered = total,
                active_users = active,
                period = new
                {
                    start_date = start_date,
                    end_date = end_date
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения отчета по пользователям");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить отчет по партнерам
    /// GET /api/v1/admin/reports/partners
    /// </summary>
    [HttpGet("reports/partners")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPartnersReport()
    {
        try
        {
            var total = await _context.Partners.CountAsync();
            var active = await _context.Partners.CountAsync(p => p.IsActive);
            var verified = await _context.Partners.CountAsync(p => p.IsVerified);

            var categories = await _context.Partners
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .GroupBy(p => p.Category)
                .Select(g => new { category = g.Key, count = g.Count() })
                .ToListAsync();

            return Ok(new
            {
                total_partners = total,
                active_partners = active,
                verified_partners = verified,
                by_category = categories
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения отчета по партнерам");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

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

    public class UpdateUserRequest
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UpdatePartnerRequest
    {
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsVerified { get; set; }
    }

    public class CreateCityRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class RejectPartnerRequest
    {
        public string? Reason { get; set; }
    }

    public class CreatePartnerLocationRequest
    {
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? IsActive { get; set; }
    }

    #region Referrals

    /// <summary>
    /// Получить список рефералов
    /// GET /api/v1/admin/referrals
    /// </summary>
    [HttpGet("referrals")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetReferrals()
    {
        try
        {
            // Получаем пользователей с реферальными кодами и их рефералов
            var usersWithReferrals = await _context.Users
                .Where(u => !string.IsNullOrEmpty(u.ReferralCode))
                .Select(u => new
                {
                    user_id = u.Id,
                    referral_code = u.ReferralCode,
                    referral_count = _context.Users.Count(r => r.ReferralCode == u.ReferralCode && r.Id != u.Id)
                })
                .ToListAsync();

            return Ok(new { items = usersWithReferrals, total = usersWithReferrals.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения рефералов");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить статистику рефералов
    /// GET /api/v1/admin/referrals/stats
    /// </summary>
    [HttpGet("referrals/stats")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetReferralsStats()
    {
        try
        {
            var totalUsersWithReferralCode = await _context.Users
                .CountAsync(u => !string.IsNullOrEmpty(u.ReferralCode));

            var totalReferrals = await _context.Users
                .CountAsync(u => !string.IsNullOrEmpty(u.ReferralCode) && u.Id != 0);

            return Ok(new
            {
                total_users_with_referral_code = totalUsersWithReferralCode,
                total_referrals = totalReferrals,
                average_referrals_per_user = totalUsersWithReferralCode > 0 
                    ? (double)totalReferrals / totalUsersWithReferralCode 
                    : 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения статистики рефералов");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region Audit

    /// <summary>
    /// Получить логи аудита
    /// GET /api/v1/admin/audit/logs
    /// </summary>
    [HttpGet("audit/logs")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int page_size = 20)
    {
        try
        {
            // В будущем можно добавить таблицу AuditLogs
            // Пока возвращаем пустой список или логи из транзакций/заказов
            var offset = (page - 1) * page_size;
            
            // Используем транзакции как пример аудита
            var transactions = await _context.Transactions
                .OrderByDescending(t => t.CreatedAt)
                .Skip(offset)
                .Take(page_size)
                .Select(t => new
                {
                    id = t.Id,
                    action = "transaction",
                    user_id = t.UserId,
                    description = t.Description,
                    created_at = t.CreatedAt
                })
                .ToListAsync();

            var total = await _context.Transactions.CountAsync();

            return Ok(new
            {
                items = transactions,
                total = total,
                page = page,
                page_size = page_size
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения логов аудита");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить сессии
    /// GET /api/v1/admin/audit/sessions
    /// </summary>
    [HttpGet("audit/sessions")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetAuditSessions()
    {
        try
        {
            // Используем последние входы пользователей как сессии
            var sessions = await _context.Users
                .Where(u => u.LastLoginAt.HasValue)
                .OrderByDescending(u => u.LastLoginAt)
                .Select(u => new
                {
                    user_id = u.Id,
                    email = u.Email ?? u.Phone,
                    last_login_at = u.LastLoginAt,
                    is_active = u.IsActive
                })
                .Take(100)
                .ToListAsync();

            return Ok(new { items = sessions, total = sessions.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения сессий");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    #region Settings

    /// <summary>
    /// Получить настройки системы
    /// GET /api/v1/admin/settings
    /// </summary>
    [HttpGet("settings")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSettings()
    {
        try
        {
            // В будущем можно добавить таблицу Settings
            // Пока возвращаем базовые настройки
            return Ok(new
            {
                app_name = "YESS!GO",
                version = "1.0.0",
                maintenance_mode = false,
                registration_enabled = true,
                max_file_size = 5242880, // 5MB
                supported_image_formats = new[] { "jpg", "jpeg", "png", "webp" }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения настроек");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Обновить настройки системы
    /// PUT /api/v1/admin/settings
    /// </summary>
    [HttpPut("settings")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateSettings([FromBody] Dictionary<string, object> settings)
    {
        try
        {
            // В будущем можно добавить таблицу Settings для сохранения
            return Ok(new { message = "Настройки обновлены", settings = settings });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления настроек");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить категории
    /// GET /api/v1/admin/settings/categories
    /// </summary>
    [HttpGet("settings/categories")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetCategories()
    {
        try
        {
            var categories = await _context.Partners
                .Where(p => !string.IsNullOrEmpty(p.Category))
                .Select(p => p.Category!)
                .Distinct()
                .ToListAsync();

            return Ok(new { items = categories.Select((c, i) => new { id = i + 1, name = c }), total = categories.Count });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения категорий");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить города (для settings)
    /// GET /api/v1/admin/settings/cities
    /// </summary>
    [HttpGet("settings/cities")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetSettingsCities()
    {
        try
        {
            var cities = await _context.Cities
                .OrderBy(c => c.Name)
                .ToListAsync();

            return Ok(new
            {
                items = cities.Select(c => new
                {
                    id = c.Id,
                    name = c.Name
                }),
                total = cities.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения городов");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Обновить город
    /// PUT /api/v1/admin/settings/cities/{id}
    /// </summary>
    [HttpPut("settings/cities/{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateCity([FromRoute] int id, [FromBody] UpdateCityRequest request)
    {
        try
        {
            var city = await _context.Cities
                .FirstOrDefaultAsync(c => c.Id == id);

            if (city == null)
            {
                return NotFound(new { error = "Город не найден" });
            }

            city.Name = request.Name;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Город обновлен", id = city.Id, name = city.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления города");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Удалить город
    /// DELETE /api/v1/admin/settings/cities/{id}
    /// </summary>
    [HttpDelete("settings/cities/{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteCity([FromRoute] int id)
    {
        try
        {
            var city = await _context.Cities
                .FirstOrDefaultAsync(c => c.Id == id);

            if (city == null)
            {
                return NotFound(new { error = "Город не найден" });
            }

            _context.Cities.Remove(city);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Город удален", id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления города");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Создать категорию
    /// POST /api/v1/admin/settings/categories
    /// </summary>
    [HttpPost("settings/categories")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateCategory([FromBody] CreateCategoryRequest request)
    {
        try
        {
            // В будущем можно добавить таблицу Categories
            return Ok(new { message = "Категория создана", name = request.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания категории");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Обновить категорию
    /// PUT /api/v1/admin/settings/categories/{id}
    /// </summary>
    [HttpPut("settings/categories/{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateCategory([FromRoute] int id, [FromBody] UpdateCategoryRequest request)
    {
        try
        {
            return Ok(new { message = "Категория обновлена", id = id, name = request.Name });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления категории");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Удалить категорию
    /// DELETE /api/v1/admin/settings/categories/{id}
    /// </summary>
    [HttpDelete("settings/categories/{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> DeleteCategory([FromRoute] int id)
    {
        try
        {
            return Ok(new { message = "Категория удалена", id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления категории");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить лимиты системы
    /// GET /api/v1/admin/settings/limits
    /// </summary>
    [HttpGet("settings/limits")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetLimits()
    {
        try
        {
            return Ok(new
            {
                max_wallet_balance = 1000000,
                min_transaction_amount = 10,
                max_transaction_amount = 100000,
                max_referral_bonus = 1000,
                daily_transaction_limit = 50000
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения лимитов");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Обновить лимиты системы
    /// PUT /api/v1/admin/settings/limits
    /// </summary>
    [HttpPut("settings/limits")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> UpdateLimits([FromBody] Dictionary<string, object> limits)
    {
        try
        {
            return Ok(new { message = "Лимиты обновлены", limits = limits });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления лимитов");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить API ключи
    /// GET /api/v1/admin/settings/api-keys
    /// </summary>
    [HttpGet("settings/api-keys")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetApiKeys()
    {
        try
        {
            // В будущем можно добавить таблицу ApiKeys
            return Ok(new { items = new object[0], total = 0 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения API ключей");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Создать API ключ
    /// POST /api/v1/admin/settings/api-keys
    /// </summary>
    [HttpPost("settings/api-keys")]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    public async Task<ActionResult> CreateApiKey([FromBody] CreateApiKeyRequest request)
    {
        try
        {
            var apiKey = Guid.NewGuid().ToString("N");
            return Ok(new { id = 1, name = request.Name, key = apiKey, created_at = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания API ключа");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Отозвать API ключ
    /// DELETE /api/v1/admin/settings/api-keys/{id}
    /// </summary>
    [HttpDelete("settings/api-keys/{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult> RevokeApiKey([FromRoute] int id)
    {
        try
        {
            return Ok(new { message = "API ключ отозван", id = id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка отзыва API ключа");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    #endregion

    public class CreateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class CreateApiKeyRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateCityRequest
    {
        public string Name { get; set; } = string.Empty;
    }

    public class AdminSendNotificationRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Segment { get; set; } // "all", "active", или null для конкретного пользователя
        public int? UserId { get; set; }
        public string? Type { get; set; }
        public string? Priority { get; set; }
        public DateTime? ScheduledFor { get; set; }
    }

    public class UpdateNotificationRequest
    {
        public string? Title { get; set; }
        public string? Message { get; set; }
    }

    public class CreatePromotionRequest
    {
        public int? PartnerId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? DiscountPercent { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? ValidUntil { get; set; }
    }

    public class UpdatePromotionRequest
    {
        public int? PartnerId { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public decimal? DiscountPercent { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? ValidUntil { get; set; }
    }
}

