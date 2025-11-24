using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер поиска пользователей для партнера
/// Соответствует /api/v1/partner/users из Python API
/// </summary>
[ApiController]
[Route("api/v1/partner/users")]
[Tags("Partner Users")]
[Authorize]
public class PartnerUsersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PartnerUsersController> _logger;

    public PartnerUsersController(
        ApplicationDbContext context,
        ILogger<PartnerUsersController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Поиск пользователей
    /// GET /api/v1/partner/users/search
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> SearchUsers(
        [FromQuery] string? search = null,
        [FromQuery] int limit = 20)
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

            var query = _context.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search) && search != "all")
            {
                query = query.Where(u =>
                    (u.Phone != null && u.Phone.Contains(search)) ||
                    (u.Email != null && u.Email.Contains(search)) ||
                    (u.FirstName != null && u.FirstName.Contains(search)) ||
                    (u.LastName != null && u.LastName.Contains(search)));
            }

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Take(limit)
                .Select(u => new
                {
                    id = u.Id,
                    phone = u.Phone,
                    email = u.Email,
                    first_name = u.FirstName,
                    last_name = u.LastName,
                    is_active = u.IsActive
                })
                .ToListAsync();

            return Ok(new { data = users });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка поиска пользователей");
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

