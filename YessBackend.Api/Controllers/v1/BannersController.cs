using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YessBackend.Infrastructure.Data;
using YessBackend.Domain.Entities; // Убедитесь, что BannersDto (или Banner) здесь

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

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult> GetBanners(
        [FromQuery] int? city_id = null,
        [FromQuery] int? partner_id = null)
    {
        try
        {
            _logger.LogInformation("Запрос баннеров: CityId={CityId}, PartnerId={PartnerId}", city_id, partner_id);

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

    /// <summary>
    /// Загрузить новый баннер
    /// </summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")] // Критично для Swagger
    public async Task<IActionResult> UploadBanner(
        IFormFile file,               // Убрали [FromForm], Swagger поймет это сам
        [FromForm] string title,
        [FromForm] int? city_id,
        [FromForm] int? partner_id)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Файл не выбран" });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { error = "Неподдерживаемый формат файла" });

            // 1. Уникальное имя
            var fileName = $"{Guid.NewGuid()}{extension}";

            // 2. Используем базовый путь приложения (надежнее для Docker)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var directoryPath = Path.Combine(baseDir, "Storage", "Banners");

            if (!Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            var filePath = Path.Combine(directoryPath, fileName);

            // 3. Сохранение
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 4. База данных
            // ПРИМЕЧАНИЕ: Если ваш класс в базе называется BannersDto, оставьте так. 
            // Но обычно это Entity класс (например, Banner).
            var banner = new BannersDto
            {
                Title = title,
                ImageFileName = fileName,
                CityId = city_id,
                PartnerId = partner_id,
                IsActive = true
            };

            _context.Banners.Add(banner);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Баннер успешно загружен",
                id = banner.Id,
                fileName = fileName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке файла");
            return StatusCode(500, new { error = "Ошибка сервера при сохранении файла" });
        }
    }
}