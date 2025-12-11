using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using YessBackend.Application.Services;
using YessBackend.Application.DTOs.Uploads;

namespace YessBackend.Api.Controllers.v1
{
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
        /// POST /api/v1/admin/upload/avatar
        /// </summary>
        [HttpPost("avatar")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> UploadAvatar([FromForm] UploadFileRequest request)
        {
            try
            {
                var adminId = GetCurrentAdminId();
                if (adminId == null)
                {
                    return Unauthorized(new { error = "Неверный токен" });
                }

                if (request.File == null || request.File.Length == 0)
                {
                    return BadRequest(new { error = "Файл не предоставлен" });
                }

                var avatarUrl = await _storageService.SaveFileAsync(request.File, "profiles");

                return Ok(new
                {
                    success = true,
                    avatar_url = avatarUrl,
                    message = "Avatar uploaded successfully"
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Ошибка загрузки аватара");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки аватара");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        /// <summary>
        /// Универсальная загрузка файлов
        /// POST /api/v1/admin/upload/file
        /// Параметры: file (IFormFile), folder (string, опционально, по умолчанию "temp")
        /// Возвращает URL файла для сохранения в БД
        /// </summary>
        [HttpPost("file")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> UploadFile([FromForm] UploadFileRequest request)
        {
            try
            {
                var adminId = GetCurrentAdminId();
                if (adminId == null)
                {
                    return Unauthorized(new { error = "Неверный токен" });
                }

                if (request.File == null || request.File.Length == 0)
                {
                    return BadRequest(new { error = "Файл не предоставлен" });
                }

                // Определяем папку для сохранения
                // Если не указана, используем "temp"
                var folder = !string.IsNullOrWhiteSpace(request.Folder) 
                    ? request.Folder.ToLowerInvariant() 
                    : "temp";

                // Валидация названия папки (только безопасные символы)
                var allowedFolders = new[] { "partners", "profiles", "temp", "qrcodes", "documents" };
                if (!allowedFolders.Contains(folder))
                {
                    return BadRequest(new { error = $"Недопустимая папка. Разрешены: {string.Join(", ", allowedFolders)}" });
                }

                // Сохраняем файл
                var fileUrl = await _storageService.SaveFileAsync(request.File, folder);

                _logger.LogInformation("Файл загружен администратором {AdminId} в папку {Folder}: {FileUrl}", 
                    adminId, folder, fileUrl);

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
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Ошибка загрузки файла");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки файла");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        /// <summary>
        /// Загрузка логотипа партнера
        /// POST /api/v1/admin/upload/partner/logo
        /// </summary>
        [HttpPost("partner/logo")]
        [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Consumes("multipart/form-data")]
        public async Task<ActionResult> UploadPartnerLogo([FromForm] UploadFileRequest request)
        {
            try
            {
                var adminId = GetCurrentAdminId();
                if (adminId == null)
                {
                    return Unauthorized(new { error = "Неверный токен" });
                }

                if (request.File == null || request.File.Length == 0)
                {
                    return BadRequest(new { error = "Файл не предоставлен" });
                }

                var logoUrl = await _storageService.SaveFileAsync(request.File, "partners");

                _logger.LogInformation("Логотип партнера загружен администратором {AdminId}: {LogoUrl}", 
                    adminId, logoUrl);

                return Ok(new
                {
                    success = true,
                    url = logoUrl,
                    logo_url = logoUrl, // Для обратной совместимости
                    message = "Логотип успешно загружен"
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Ошибка загрузки логотипа партнера");
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка загрузки логотипа партнера");
                return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
            }
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("user_id")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(userIdClaim, out var userId)
                ? userId
                : null;
        }

        private Guid? GetCurrentAdminId()
        {
            var adminIdClaim = User.FindFirst("admin_id")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return Guid.TryParse(adminIdClaim, out var adminId)
                ? adminId
                : null;
        }
    }
}
