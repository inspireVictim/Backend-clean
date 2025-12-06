using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using YessBackend.Application.Services;
using YessBackend.Application.DTOs;

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
        public async Task<ActionResult> UploadAvatar([FromForm] UploadAvatarRequest request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (userId == null)
                {
                    return Unauthorized(new { error = "Неверный токен" });
                }

                if (request.Avatar == null || request.Avatar.Length == 0)
                {
                    return BadRequest(new { error = "Файл не предоставлен" });
                }

                var avatarUrl = await _storageService.SaveFileAsync(request.Avatar, "profiles");

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

        private int? GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst("user_id")?.Value
                ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            return int.TryParse(userIdClaim, out var userId)
                ? userId
                : null;
        }
    }
}
