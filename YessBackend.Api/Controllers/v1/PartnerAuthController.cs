using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YessBackend.Application.DTOs.Auth;
using YessBackend.Application.DTOs.PartnerAuth;
using YessBackend.Application.Services;
using YessBackend.Infrastructure.Data;
using YessBackend.Application.DTOs.Partner;
using YessBackend.Application.DTOs.PartnerAuth;
using Microsoft.EntityFrameworkCore;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер аутентификации партнера
/// Соответствует /api/v1/partner/auth из Python API
/// </summary>
[ApiController]
[Route("api/v1/partner/auth")]
[Tags("Partner Authentication")]
public class PartnerAuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PartnerAuthController> _logger;

    public PartnerAuthController(
        IAuthService authService,
        ApplicationDbContext context,
        ILogger<PartnerAuthController> logger)
    {
        _authService = authService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Аутентификация партнера
    /// POST /api/v1/partner/auth/login
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<TokenResponseDto>> PartnerLogin([FromBody] PartnerLoginRequestDto request)
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

            // Проверяем, является ли пользователь партнером
            var partnerEmployee = await _context.PartnerEmployees
                .FirstOrDefaultAsync(pe => pe.UserId == user.Id);

            var partner = await _context.Partners
                .FirstOrDefaultAsync(p => p.OwnerId == user.Id || (partnerEmployee != null && p.Id == partnerEmployee.PartnerId));

            if (partner == null)
            {
                return Unauthorized(new { error = "Пользователь не является партнером" });
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

            var tokenResponse = new TokenResponseDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "bearer",
                ExpiresIn = 3600 // 1 час
            };

            return Ok(tokenResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка входа партнера");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Регистрация партнера
    /// POST /api/v1/partner/auth/register
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(PartnerResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<PartnerResponseDto>> PartnerRegister([FromBody] PartnerRegisterRequestDto request)
    {
    try
    {
        // Валидация модели (проверка обязательных полей)
        if (!ModelState.IsValid)
        {
            var errors = ModelState
                .Where(x => x.Value?.Errors.Count > 0)
                .SelectMany(x => x.Value!.Errors.Select(e => new { Field = x.Key, Message = e.ErrorMessage }))
                .ToList();

            return BadRequest(new { error = "Ошибка валидации", errors = errors });
        }

        // Проверяем, существует ли пользователь с таким телефоном
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Phone == request.Phone);

        if (existingUser != null)
        {
            return BadRequest(new { error = "Пользователь с таким телефоном уже существует" });
        }

        // Проверяем, существует ли пользователь с таким email (если email указан)
        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            var existingUserByEmail = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == request.Email);

            if (existingUserByEmail != null)
            {
                return BadRequest(new { error = "Пользователь с таким email уже существует" });
            }
        }

        // Проверяем, существует ли город
        var city = await _context.Cities
            .FirstOrDefaultAsync(c => c.Id == request.CityId);

        if (city == null)
        {
            return BadRequest(new { error = "Город не найден" });
        }

        // Хешируем пароль
        var passwordHash = _authService.HashPassword(request.Password);

        // Создаем пользователя (владельца партнера)
        var user = new Domain.Entities.User
        {
            Phone = request.Phone,
            Email = request.Email ?? string.Empty, // Если email не указан, используем пустую строку
            PasswordHash = passwordHash,
            FirstName = request.Name, // Используем название партнера как имя
            LastName = "", // Можно оставить пустым или добавить отдельное поле
            CityId = request.CityId,
            IsActive = true,
            PhoneVerified = false, // Партнер должен будет верифицировать телефон позже
            EmailVerified = !string.IsNullOrWhiteSpace(request.Email), // Если email указан, считаем верифицированным
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync(); // Сохраняем пользователя, чтобы получить его ID

        // Создаем словарь для социальных сетей (для хранения ссылки на 2GIS)
        var socialMedia = new Dictionary<string, string>
        {
            { "2gis", request.TwoGisUrl }
        };

        // Создаем партнера
        var partner = new Domain.Entities.Partner
        {
            Name = request.Name,
            Category = request.Category,
            Description = request.Description,
            Phone = request.Phone,
            Email = request.Email,
            Website = request.Website,
            LogoUrl = request.LogoUrl,
            CoverImageUrl = request.CoverImageUrl,
            CityId = request.CityId,
            MaxDiscountPercent = request.MaxDiscountPercent,
            CashbackRate = request.CashbackRate ?? 5.0m, // По умолчанию 5%
            DefaultCashbackRate = request.CashbackRate ?? 5.0m,
            OwnerId = user.Id, // Связываем партнера с пользователем
            SocialMedia = socialMedia, // Сохраняем ссылку на 2GIS
            IsActive = false, // Партнер неактивен до верификации администратором
            IsVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Partners.Add(partner);
        await _context.SaveChangesAsync(); // Сохраняем партнера, чтобы получить его ID

        // Создаем локацию партнера с адресом
        var partnerLocation = new Domain.Entities.PartnerLocation
        {
            PartnerId = partner.Id,
            Address = request.Address,
            IsActive = true,
            IsMainLocation = true, // Это основная локация партнера
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.PartnerLocations.Add(partnerLocation);
        await _context.SaveChangesAsync(); // Сохраняем локацию

        // Создаем кошелек для пользователя (если его еще нет)
        var existingWallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == user.Id);

        if (existingWallet == null)
        {
            var wallet = new Domain.Entities.Wallet
            {
                UserId = user.Id,
                Balance = 0,
                YescoinBalance = 0,
                TotalEarned = 0,
                TotalSpent = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        // Формируем ответ
        var response = new PartnerResponseDto
        {
            Id = partner.Id,
            Name = partner.Name,
            Description = partner.Description,
            Category = partner.Category,
            CityId = partner.CityId,
            LogoUrl = partner.LogoUrl,
            CoverImageUrl = partner.CoverImageUrl,
            Phone = partner.Phone,
            Email = partner.Email,
            Website = partner.Website,
            CashbackRate = partner.CashbackRate,
            MaxDiscountPercent = partner.MaxDiscountPercent,
            IsActive = partner.IsActive,
            IsVerified = partner.IsVerified,
            CreatedAt = partner.CreatedAt
        };

        return Ok(response);
    }
    catch (DbUpdateException ex)
    {
        _logger.LogError(ex, "Ошибка сохранения данных партнера");
        return StatusCode(500, new { error = "Ошибка сохранения данных. Возможно, партнер с такими данными уже существует." });
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Ошибка регистрации партнера");
        return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
    }
}
}

