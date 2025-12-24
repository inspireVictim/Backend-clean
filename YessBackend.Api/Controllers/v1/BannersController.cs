using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Api.Controllers.v1;

[ApiController]
[Route("api/v1/banners")]
[Tags("Banners")]
public class BannersController : ControllerBase
{
    private readonly ILogger<BannersController> _logger;
    private readonly ApplicationDbContext _context;

    public BannersController(ILogger<BannersController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    /// <summary>
    /// Получить активные баннеры с фильтрацией по городу и партнеру
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetBanners(
        [FromQuery] int? city_id = null,
        [FromQuery] int? partner_id = null)
    {
        try
        {
            _logger.LogInformation("Запрос баннеров: CityId={CityId}, PartnerId={PartnerId}", city_id, partner_id);

            // Базовый путь для формирования ссылок
            var baseUrl = $"{Request.Scheme}://{Request.Host}/content/banners/";

            var query = _context.Banners.AsQueryable().Where(b => b.IsActive);

            if (city_id.HasValue)
                query = query.Where(b => b.CityId == city_id);

            if (partner_id.HasValue)
                query = query.Where(b => b.PartnerId == partner_id);

            var banners = await query.ToListAsync();

            var result = banners.Select(b => new
            {
                id = b.Id,
                title = b.Title,
                image_url = baseUrl + b.ImageFileName,
                city_id = b.CityId,
                partner_id = b.PartnerId
            });

            return Ok(new
            {
                items = result,
                total = result.Count()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при получении баннеров");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }
}

