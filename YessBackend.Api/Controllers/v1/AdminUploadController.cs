using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using YessBackend.Application.Services;
using YessBackend.Application.DTOs.Uploads;

namespace YessBackend.Api.Controllers.v1
{
    // Определение DTO, если оно отсутствует в YessBackend.Application.DTOs.Uploads
    // Если класс уже есть в другом файле, убедитесь, что имена совпадают.
    public class UploadFileRequest
    {
        public IFormFile File { get; set; } = null!;
        public string? Folder { get; set; }
    }

    /// <summary>
    /// Контроллер загрузки файлов для админ-панели
    /// </summary>
    [ApiController]
    [Route("api/v1/admin/upload")]
    [Tags("Admin File Upload")]
    [Authorize]
    public class AdminUploadController : ControllerBase
    {
        private readonly IStorageService _storageService;
        private readonly ILogger<AdminUploadController> _logger;

        public AdminUploadController(
            IStorageService storageService,
            ILogger<AdminUploadController> logger)
        {
            _storageService = storageService;
            _logger = logger;
        }

        /// <summary>
        /// Загрузка аватара администратора
        /// </summary>
        [HttpPost("avatar")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> UploadAvatar([FromForm] UploadFileRequest request)
        {
            try
            {
                var adminId = GetCurrentAdminId();
                if (adminId == null) return Unauthorized(new { error = "Неверный токен" });

                if (request.File == null || request.File.Length == 0)
                    return BadRequest(new { error = "Файл не предоставлен" });

                var avatarUrl = await _storageService.SaveFileAsync(request.File, "profiles");

                return Ok(new
                {
                    success = true,
                    avatar_url = avatarUrl,
                    message = "Avatar uploaded successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки аватара");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        /// <summary>
        /// Универсальная загрузка файлов
        /// </summary>
        [HttpPost("file")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> UploadFile([FromForm] UploadFileRequest request)
        {
            try
            {
                var adminId = GetCurrentAdminId();
                if (adminId == null) return Unauthorized(new { error = "Неверный токен" });

                if (request.File == null || request.File.Length == 0)
                    return BadRequest(new { error = "Файл не предоставлен" });

                var folder = !string.IsNullOrWhiteSpace(request.Folder)
                    ? request.Folder.ToLowerInvariant()
                    : "temp";

                var allowedFolders = new[] { "partners", "profiles", "temp", "qrcodes", "documents" };
                if (!allowedFolders.Contains(folder))
                    return BadRequest(new { error = $"Недопустимая папка. Разрешены: {string.Join(", ", allowedFolders)}" });

                var fileUrl = await _storageService.SaveFileAsync(request.File, folder);

                return Ok(new
                {
                    success = true,
                    url = fileUrl,
                    folder = folder,
                    filename = request.File.FileName,
                    size = request.File.Length,
                    message = "Файл успешно загружен"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки файла");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        /// <summary>
        /// Загрузка логотипа партнера
        /// </summary>
        [HttpPost("partner/logo")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> UploadPartnerLogo([FromForm] UploadFileRequest request)
        {
            try
            {
                var adminId = GetCurrentAdminId();
                if (adminId == null) return Unauthorized(new { error = "Неверный токен" });

                if (request.File == null || request.File.Length == 0)
                    return BadRequest(new { error = "Файл не предоставлен" });

                var logoUrl = await _storageService.SaveFileAsync(request.File, "partners");

                return Ok(new
                {
                    success = true,
                    url = logoUrl,
                    logo_url = logoUrl,
                    message = "Логотип успешно загружен"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки логотипа");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        private Guid? GetCurrentAdminId()
        {
            var adminIdClaim = User.FindFirst("admin_id")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(adminIdClaim, out var adminId) ? adminId : null;
        }
    }
}
