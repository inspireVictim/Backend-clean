using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер локаций партнера
/// Соответствует /api/v1/partner/locations из Python API
/// </summary>
[ApiController]
[Route("api/v1/partner/locations")]
[Tags("Partner Locations")]
[Authorize]
public class PartnerLocationsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PartnerLocationsController> _logger;

    public PartnerLocationsController(
        ApplicationDbContext context,
        ILogger<PartnerLocationsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Получить локации текущего партнера
    /// GET /api/v1/partner/locations
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetLocations()
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

            var locations = await _context.PartnerLocations
                .Where(l => l.PartnerId == partner.Id)
                .ToListAsync();

            return Ok(new
            {
                data = locations.Select(l => new
                {
                    id = l.Id,
                    partner_id = l.PartnerId,
                    address = l.Address,
                    latitude = l.Latitude,
                    longitude = l.Longitude,
                    phone_number = l.PhoneNumber,
                    is_active = l.IsActive,
                    created_at = l.CreatedAt
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения локаций партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Создать локацию партнера
    /// POST /api/v1/partner/locations
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> CreateLocation([FromBody] CreateLocationRequest request)
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

            var location = new Domain.Entities.PartnerLocation
            {
                PartnerId = partner.Id,
                Address = request.Address,
                Latitude = (decimal?)request.Latitude,
                Longitude = (decimal?)request.Longitude,
                PhoneNumber = request.PhoneNumber,
                IsActive = request.IsActive ?? true,
                CreatedAt = DateTime.UtcNow
            };

            _context.PartnerLocations.Add(location);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetLocations), new { }, new
            {
                data = new
                {
                    id = location.Id,
                    partner_id = location.PartnerId,
                    address = location.Address,
                    latitude = location.Latitude,
                    longitude = location.Longitude,
                    phone_number = location.PhoneNumber,
                    is_active = location.IsActive,
                    created_at = location.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания локации партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Обновить локацию партнера
    /// PUT /api/v1/partner/locations/{id}
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> UpdateLocation([FromRoute] int id, [FromBody] UpdateLocationRequest request)
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

            var location = await _context.PartnerLocations
                .FirstOrDefaultAsync(l => l.Id == id && l.PartnerId == partner.Id);

            if (location == null)
            {
                return NotFound(new { error = "Локация не найдена" });
            }

            if (!string.IsNullOrEmpty(request.Address))
            {
                location.Address = request.Address;
            }

            if (request.Latitude.HasValue)
            {
                location.Latitude = (decimal?)request.Latitude.Value;
            }

            if (request.Longitude.HasValue)
            {
                location.Longitude = (decimal?)request.Longitude.Value;
            }

            if (request.PhoneNumber != null)
            {
                location.PhoneNumber = request.PhoneNumber;
            }

            if (request.IsActive.HasValue)
            {
                location.IsActive = request.IsActive.Value;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                data = new
                {
                    id = location.Id,
                    partner_id = location.PartnerId,
                    address = location.Address,
                    latitude = location.Latitude,
                    longitude = location.Longitude,
                    phone_number = location.PhoneNumber,
                    is_active = location.IsActive
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления локации партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Удалить локацию партнера
    /// DELETE /api/v1/partner/locations/{id}
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeleteLocation([FromRoute] int id)
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

            var location = await _context.PartnerLocations
                .FirstOrDefaultAsync(l => l.Id == id && l.PartnerId == partner.Id);

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

    public class CreateLocationRequest
    {
        public string Address { get; set; } = string.Empty;
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? IsActive { get; set; }
    }

    public class UpdateLocationRequest
    {
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? PhoneNumber { get; set; }
        public bool? IsActive { get; set; }
    }
}

