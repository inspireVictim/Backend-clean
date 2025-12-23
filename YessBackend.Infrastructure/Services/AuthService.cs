using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using YessBackend.Application.DTOs.Auth;
using YessBackend.Application.Services;
using YessBackend.Domain.Entities;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Infrastructure.Services;

/// <summary>
/// Сервис аутентификации
/// Реализует логику регистрации с сохранением кода агента в ReferredBy
/// </summary>
public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService>? _logger;

    public AuthService(ApplicationDbContext context, IConfiguration configuration, ILogger<AuthService>? logger = null)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<User> RegisterUserAsync(UserRegisterDto userDto)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Phone == userDto.PhoneNumber);

        if (existingUser != null)
        {
            throw new InvalidOperationException("Пользователь с таким номером телефона уже существует");
        }

        var referralCode = GenerateUniqueReferralCode();

        int? cityId = null;
        if (userDto.CityId.HasValue && userDto.CityId.Value > 0)
        {
            cityId = userDto.CityId.Value;
        }

        var user = new User
        {
            Email = $"{userDto.PhoneNumber}@example.local",
            Phone = userDto.PhoneNumber,
            FirstName = userDto.FirstName,
            LastName = userDto.LastName,
            PasswordHash = HashPassword(userDto.Password),
            CityId = cityId,
            ReferralCode = referralCode,
            // Сохраняем строку напрямую для сетевого маркетинга
            ReferredBy = userDto.ReferralCode,
            PhoneVerified = false,
            EmailVerified = false,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var wallet = new Wallet
        {
            UserId = user.Id,
            Balance = 0.0m,
            YescoinBalance = 0.0m,
            TotalEarned = 0.0m,
            TotalSpent = 0.0m,
            LastUpdated = DateTime.UtcNow
        };

        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<User> VerifyCodeAndRegisterAsync(VerifyCodeAndRegisterRequestDto requestDto)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Phone == requestDto.PhoneNumber);

        if (user == null)
        {
            throw new InvalidOperationException("Код верификации не найден. Сначала отправьте код верификации.");
        }

        if (string.IsNullOrEmpty(user.VerificationCode))
        {
            throw new InvalidOperationException("Код верификации не найден. Сначала отправьте код верификации.");
        }

        if (user.VerificationExpiresAt.HasValue && user.VerificationExpiresAt.Value < DateTime.UtcNow)
        {
            throw new InvalidOperationException("Срок действия кода верификации истек. Отправьте новый код.");
        }

        if (user.VerificationCode != requestDto.Code)
        {
            throw new InvalidOperationException("Неверный код верификации");
        }

        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new InvalidOperationException("Пользователь с таким номером телефона уже зарегистрирован");
        }

        user.PasswordHash = HashPassword(requestDto.Password);
        user.FirstName = requestDto.FirstName;
        user.LastName = requestDto.LastName;
        user.Name = $"{requestDto.FirstName} {requestDto.LastName}";

        if (requestDto.CityId.HasValue && requestDto.CityId.Value > 0)
        {
            user.CityId = requestDto.CityId.Value;
        }

        // ИСПРАВЛЕНО: Сохраняем введенный код агента напрямую как строку
        if (!string.IsNullOrEmpty(requestDto.ReferralCode))
        {
            user.ReferredBy = requestDto.ReferralCode;
        }

        if (string.IsNullOrEmpty(user.ReferralCode))
        {
            user.ReferralCode = GenerateUniqueReferralCode();
        }

        user.VerificationCode = null;
        user.PhoneVerified = true;
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var existingWallet = await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == user.Id);

        if (existingWallet == null)
        {
            var wallet = new Wallet
            {
                UserId = user.Id,
                Balance = 0.0m,
                YescoinBalance = 0.0m,
                TotalEarned = 0.0m,
                TotalSpent = 0.0m,
                LastUpdated = DateTime.UtcNow
            };
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        return user;
    }

    // --- Остальные методы оставлены без изменений для корректной работы JWT и паролей ---

    public async Task<TokenResponseDto> LoginAsync(UserLoginDto loginDto)
    {
        var user = await GetUserByPhoneAsync(loginDto.Phone);
        if (user == null) throw new InvalidOperationException("Пользователь не найден");
        if (!VerifyPassword(loginDto.Password, user.PasswordHash ?? string.Empty)) throw new InvalidOperationException("Неверный пароль");

        user.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new TokenResponseDto
        {
            AccessToken = CreateAccessToken(user),
            RefreshToken = CreateRefreshToken(user),
            TokenType = "bearer",
            ExpiresIn = _configuration.GetValue<int>("Jwt:AccessTokenExpireMinutes", 60) * 60
        };
    }

    public async Task<User?> GetUserByPhoneAsync(string phone) =>
        await _context.Users.FirstOrDefaultAsync(u => u.Phone == phone);

    public async Task<User?> GetUserByIdAsync(int userId) =>
        await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);

    public async Task<string> SendVerificationCodeAsync(string phoneNumber)
    {
        var random = new Random();
        var code = random.Next(100000, 999999).ToString("D6");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == phoneNumber);

        if (user != null)
        {
            if (!string.IsNullOrEmpty(user.PasswordHash)) throw new InvalidOperationException("Пользователь уже зарегистрирован");
            user.VerificationCode = code;
            user.VerificationExpiresAt = DateTime.UtcNow.AddMinutes(10);
            await _context.SaveChangesAsync();
        }
        else
        {
            var tempUser = new User
            {
                Phone = phoneNumber,
                Email = $"{phoneNumber}@temp.local",
                VerificationCode = code,
                VerificationExpiresAt = DateTime.UtcNow.AddMinutes(10),
                PhoneVerified = false,
                IsActive = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.Users.Add(tempUser);
            await _context.SaveChangesAsync();
        }
        return code;
    }

    public async Task<ReferralStatsResponseDto> GetReferralStatsAsync(int userId)
    {
        // ВНИМАНИЕ: Статистика будет работать только если ReferredBy содержит ID. 
        // Если там теперь строка кода, этот метод нужно будет переделать под поиск по строке.
        var user = await GetUserByIdAsync(userId);
        var totalReferred = await _context.Users.CountAsync(u => u.ReferredBy == user.ReferralCode);

        return new ReferralStatsResponseDto
        {
            TotalReferred = totalReferred,
            ReferralCode = user?.ReferralCode
        };
    }

    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);

    public string CreateAccessToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim("user_id", user.Id.ToString()), new Claim("phone", user.Phone) };

        var token = new JwtSecurityToken(jwtSettings["Issuer"], jwtSettings["Audience"], claims,
            expires: DateTime.UtcNow.AddMinutes(jwtSettings.GetValue<int>("AccessTokenExpireMinutes", 60)), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[] { new Claim("user_id", user.Id.ToString()), new Claim("type", "refresh") };

        var token = new JwtSecurityToken(jwtSettings["Issuer"], jwtSettings["Audience"], claims,
            expires: DateTime.UtcNow.AddDays(7), signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateUniqueReferralCode(int length = 8)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var code = new string(Enumerable.Repeat(chars, length).Select(s => s[random.Next(s.Length)]).ToArray());
        return _context.Users.Any(u => u.ReferralCode == code) ? GenerateUniqueReferralCode() : code;
    }

    public async Task<User?> UpdateUserAsync(int userId, UpdateProfileRequestDto dto)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return null;
        if (!string.IsNullOrWhiteSpace(dto.FirstName)) user.FirstName = dto.FirstName;
        if (!string.IsNullOrWhiteSpace(dto.LastName)) user.LastName = dto.LastName;
        await _context.SaveChangesAsync();
        return user;
    }
}