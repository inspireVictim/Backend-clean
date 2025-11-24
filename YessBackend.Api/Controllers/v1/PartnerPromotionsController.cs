using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using YessBackend.Application.Services;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер промо-акций партнера
/// Соответствует /api/v1/partner/promotions из Python API
/// </summary>
[ApiController]
[Route("api/v1/partner/promotions")]
[Tags("Partner Promotions")]
[Authorize]
public class PartnerPromotionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly IPromotionService _promotionService;
    private readonly ILogger<PartnerPromotionsController> _logger;

    public PartnerPromotionsController(
        ApplicationDbContext context,
        IPromotionService promotionService,
        ILogger<PartnerPromotionsController> logger)
    {
        _context = context;
        _promotionService = promotionService;
        _logger = logger;
    }

    /// <summary>
    /// Получить промо-акции текущего партнера
    /// GET /api/v1/partner/promotions
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> GetPromotions()
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

            var promotions = await _context.Promotions
                .Where(p => p.PartnerId == partner.Id)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    id = p.Id,
                    partner_id = p.PartnerId,
                    title = p.Title,
                    description = p.Description,
                    discount_percent = p.DiscountPercent,
                    is_active = p.IsActive,
                    valid_until = p.ValidUntil,
                    created_at = p.CreatedAt
                })
                .ToListAsync();

            return Ok(new { data = promotions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения промо-акций партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Создать промо-акцию партнера
    /// POST /api/v1/partner/promotions
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> CreatePromotion([FromBody] CreatePartnerPromotionRequest request)
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

            var promotion = new Domain.Entities.Promotion
            {
                PartnerId = partner.Id,
                Title = request.Title,
                Description = request.Description,
                DiscountPercent = request.DiscountPercent ?? 0m,
                IsActive = request.IsActive ?? true,
                ValidUntil = request.ValidUntil,
                CreatedAt = DateTime.UtcNow
            };

            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPromotions), new { }, new
            {
                data = new
                {
                    id = promotion.Id,
                    partner_id = promotion.PartnerId,
                    title = promotion.Title,
                    description = promotion.Description,
                    discount_percent = promotion.DiscountPercent,
                    is_active = promotion.IsActive,
                    valid_until = promotion.ValidUntil,
                    created_at = promotion.CreatedAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка создания промо-акции партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Обновить промо-акцию партнера
    /// PUT /api/v1/partner/promotions/{id}
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> UpdatePromotion([FromRoute] int id, [FromBody] UpdatePartnerPromotionRequest request)
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

            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Id == id && p.PartnerId == partner.Id);

            if (promotion == null)
            {
                return NotFound(new { error = "Промо-акция не найдена" });
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
                data = new
                {
                    id = promotion.Id,
                    partner_id = promotion.PartnerId,
                    title = promotion.Title,
                    description = promotion.Description,
                    discount_percent = promotion.DiscountPercent,
                    is_active = promotion.IsActive,
                    valid_until = promotion.ValidUntil
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обновления промо-акции партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Удалить промо-акцию партнера
    /// DELETE /api/v1/partner/promotions/{id}
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult> DeletePromotion([FromRoute] int id)
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

            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.Id == id && p.PartnerId == partner.Id);

            if (promotion == null)
            {
                return NotFound(new { error = "Промо-акция не найдена" });
            }

            _context.Promotions.Remove(promotion);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Промо-акция удалена" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка удаления промо-акции партнера");
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

    public class CreatePartnerPromotionRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? DiscountPercent { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? ValidUntil { get; set; }
    }

    public class UpdatePartnerPromotionRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public decimal? DiscountPercent { get; set; }
        public bool? IsActive { get; set; }
        public DateTime? ValidUntil { get; set; }
    }
}

