using Microsoft.AspNetCore.Mvc;
using YessBackend.Application.DTOs.AdminAuth;
using YessBackend.Application.Services;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер аутентификации администратора
/// </summary>
[ApiController]
[Route("api/v1/admin/auth")]
[Tags("Admin Authentication")]
public class AdminAuthController : ControllerBase
{
    private readonly IAdminAuthService _adminAuthService;
    private readonly ILogger<AdminAuthController> _logger;

    public AdminAuthController(
        IAdminAuthService adminAuthService,
        ILogger<AdminAuthController> logger)
    {
        _adminAuthService = adminAuthService;
        _logger = logger;
    }

    /// <summary>
    /// Аутентификация администратора
    /// POST /api/v1/admin/auth/login
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AdminLoginResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AdminLoginResponseDto>> Login([FromBody] AdminLoginDto loginDto)
    {
        try
        {
            var response = await _adminAuthService.LoginAsync(loginDto);
            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Ошибка входа администратора: {Message}", ex.Message);
            return Unauthorized(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Критическая ошибка входа администратора");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Регистрация нового администратора
    /// POST /api/v1/admin/auth/register
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AdminResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminResponseDto>> Register([FromBody] AdminRegisterDto registerDto)
    {
        try
        {
            var result = await _adminAuthService.RegisterAdminAsync(registerDto);
            return CreatedAtAction(null, result);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Ошибка регистрации администратора: {Message}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка регистрации администратора");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }
}