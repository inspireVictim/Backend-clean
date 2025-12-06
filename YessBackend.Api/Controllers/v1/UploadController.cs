using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using YessBackend.Application.Services;
using YessBackend.Application.DTOs.Uploads;

namespace YessBackend.Api.Controllers.v1;

[ApiController]
[Route("api/v1/upload")]
[Tags("File Upload")]
[Authorize]
public class UploadController : ControllerBase
{
    private readonly IStorageService _storageService;
    private readonly ILogger<UploadController> _logger;

    public UploadController(
        IStorageService storageService,
        ILogger<UploadController> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    // -------------------- Upload Avatar --------------------

    [HttpPost("avatar")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> UploadAvatar([FromForm] UploadAvatarRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { error = "Неверный токен" });

            if (request.Avatar == null || request.Avatar.Length == 0)
                return BadRequest(new { error = "Файл не предоставлен" });

            var avatarUrl = await _storageService.SaveFileAsync(request.Avatar, "profiles");

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

    // -------------------- Upload Partner Logo --------------------

    [HttpPost("partner/logo/{partner_id}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> UploadPartnerLogo(
        [FromRoute] int partner_id,
        [FromForm] UploadPartnerLogoRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { error = "Неверный токен" });

            if (request.Logo == null || request.Logo.Length == 0)
                return BadRequest(new { error = "Файл не предоставлен" });

            var logoUrl = await _storageService.SaveFileAsync(request.Logo, "partners");

            return Ok(new
            {
                success = true,
                logo_url = logoUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки логотипа партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    // -------------------- Upload Partner Cover --------------------

    [HttpPost("partner/cover/{partner_id}")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult> UploadPartnerCover(
        [FromRoute] int partner_id,
        [FromForm] UploadPartnerCoverRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null)
                return Unauthorized(new { error = "Неверный токен" });

            if (request.Cover == null || request.Cover.Length == 0)
                return BadRequest(new { error = "Файл не предоставлен" });

            var coverUrl = await _storageService.SaveFileAsync(request.Cover, "partners");

            return Ok(new
            {
                success = true,
                cover_url = coverUrl
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка загрузки обложки партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    // -------------------- Helpers --------------------

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return int.TryParse(userIdClaim, out var userId)
            ? userId
            : null;
    }
}
