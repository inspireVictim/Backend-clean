using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YessBackend.Application.DTOs.Auth;
using YessBackend.Application.Services;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер аутентификации администратора
/// Соответствует /api/v1/admin/auth из Python API
/// </summary>
[ApiController]
[Route("api/v1/admin/auth")]
[Tags("Admin Authentication")]
public class AdminAuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminAuthController> _logger;

    public AdminAuthController(
        IAuthService authService,
        ApplicationDbContext context,
        ILogger<AdminAuthController> logger)
    {
        _authService = authService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Аутентификация администратора
    /// POST /api/v1/admin/auth/login
    /// Поддерживает username (phone/email) и password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> AdminLogin([FromBody] AdminLoginRequest request)
    {
        try
        {
            // Определяем, это телефон или email
            var isEmail = request.Username.Contains("@");
            
            Domain.Entities.User? user;
            if (isEmail)
            {
                user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email == request.Username);
            }
            else
            {
                user = await _context.Users
                    .FirstOrDefaultAsync(u => u.Phone == request.Username);
            }

            if (user == null)
            {
                return Unauthorized(new { error = "Неверный логин или пароль" });
            }

            // Проверяем пароль
            if (!_authService.VerifyPassword(request.Password, user.PasswordHash ?? string.Empty))
            {
                return Unauthorized(new { error = "Неверный логин или пароль" });
            }

            // Обновляем время последнего входа
            user.LastLoginAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // Создаем токены
            var accessToken = _authService.CreateAccessToken(user);
            var refreshToken = _authService.CreateRefreshToken(user);

            return Ok(new
            {
                access_token = accessToken,
                refresh_token = refreshToken,
                token_type = "bearer",
                expires_in = 3600, // 1 час
                admin = new
                {
                    id = user.Id.ToString(),
                    email = user.Email ?? user.Phone,
                    phone = user.Phone,
                    role = "admin",
                    name = (!string.IsNullOrWhiteSpace($"{user.FirstName} {user.LastName}".Trim()) 
                        ? $"{user.FirstName} {user.LastName}".Trim() 
                        : (user.Email ?? user.Phone ?? "Admin"))
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка входа администратора");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    public class AdminLoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}

