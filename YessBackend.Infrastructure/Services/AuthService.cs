using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using AutoMapper; // Добавлено
using YessBackend.Application.DTOs.Auth;
using YessBackend.Application.Services;
using YessBackend.Domain.Entities;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IMapper _mapper; // Добавлено
    private readonly ILogger<AuthService>? _logger;

    public AuthService(ApplicationDbContext context, IConfiguration configuration, IMapper mapper, ILogger<AuthService>? logger = null)
    {
        _context = context;
        _configuration = configuration;
        _mapper = mapper; // Добавлено
        _logger = logger;
    }

    // РЕАЛИЗАЦИЯ НОВОГО МЕТОДА
    public async Task<UserResponseDto?> GetUserProfileAsync(int userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        var dto = _mapper.Map<UserResponseDto>(user);

        // Подсчет количества рефералов по коду
        dto.ReferralsCount = await _context.Users
            .CountAsync(u => u.ReferredBy == user.ReferralCode);

        return dto;
    }

    public async Task<User> RegisterUserAsync(UserRegisterDto userDto)
    {
        var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Phone == userDto.PhoneNumber);
        if (existingUser != null) throw new InvalidOperationException("Пользователь уже существует");

        var referralCode = GenerateUniqueReferralCode();
        int? cityId = (userDto.CityId.HasValue && userDto.CityId.Value > 0) ? userDto.CityId.Value : null;

        var user = new User
        {
            Email = $"{userDto.PhoneNumber}@example.local",
            Phone = userDto.PhoneNumber,
            FirstName = userDto.FirstName,
            LastName = userDto.LastName,
            Name = $"{userDto.FirstName} {userDto.LastName}".Trim(),
            PasswordHash = HashPassword(userDto.Password),
            CityId = cityId,
            ReferralCode = referralCode,
            ReferredBy = userDto.ReferralCode,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var wallet = new Wallet { UserId = user.Id, Balance = 0.0m, YescoinBalance = 0.0m, LastUpdated = DateTime.UtcNow };
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();

        return user;
    }

    public async Task<TokenResponseDto> LoginAsync(UserLoginDto loginDto)
    {
        var user = await GetUserByPhoneAsync(loginDto.Phone);
        if (user == null || !VerifyPassword(loginDto.Password, user.PasswordHash ?? string.Empty))
            throw new InvalidOperationException("Неверный логин или пароль");

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
            if (!string.IsNullOrEmpty(user.PasswordHash)) throw new InvalidOperationException("Уже зарегистрирован");
            user.VerificationCode = code;
            user.VerificationExpiresAt = DateTime.UtcNow.AddMinutes(10);
        }
        else
        {
            var tempUser = new User { Phone = phoneNumber, Email = $"{phoneNumber}@temp.local", VerificationCode = code, VerificationExpiresAt = DateTime.UtcNow.AddMinutes(10), IsActive = false, CreatedAt = DateTime.UtcNow };
            _context.Users.Add(tempUser);
        }
        await _context.SaveChangesAsync();
        return code;
    }

    public async Task<User> VerifyCodeAndRegisterAsync(VerifyCodeAndRegisterRequestDto requestDto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Phone == requestDto.PhoneNumber);
        if (user == null || user.VerificationCode != requestDto.Code) throw new InvalidOperationException("Неверный код");

        user.PasswordHash = HashPassword(requestDto.Password);
        user.FirstName = requestDto.FirstName;
        user.LastName = requestDto.LastName;
        user.Name = $"{requestDto.FirstName} {requestDto.LastName}".Trim();
        user.PhoneVerified = true;
        user.UpdatedAt = DateTime.UtcNow;
        user.VerificationCode = null;
        user.ReferredBy = requestDto.ReferralCode;

        if (requestDto.CityId.HasValue && requestDto.CityId.Value > 0) user.CityId = requestDto.CityId.Value;
        if (string.IsNullOrEmpty(user.ReferralCode)) user.ReferralCode = GenerateUniqueReferralCode();

        await _context.SaveChangesAsync();

        if (!await _context.Wallets.AnyAsync(w => w.UserId == user.Id))
        {
            _context.Wallets.Add(new Wallet { UserId = user.Id, Balance = 0.0m, LastUpdated = DateTime.UtcNow });
            await _context.SaveChangesAsync();
        }

        return user;
    }

    public async Task<ReferralStatsResponseDto> GetReferralStatsAsync(int userId)
    {
        var user = await GetUserByIdAsync(userId);
        var total = await _context.Users.CountAsync(u => u.ReferredBy == user.ReferralCode);
        return new ReferralStatsResponseDto { TotalReferred = total, ReferralCode = user?.ReferralCode };
    }

    public async Task<User?> UpdateUserAsync(int userId, UpdateProfileRequestDto dto)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return null;

        if (!string.IsNullOrWhiteSpace(dto.FirstName)) user.FirstName = dto.FirstName;
        if (!string.IsNullOrWhiteSpace(dto.LastName)) user.LastName = dto.LastName;

        user.Name = $"{user.FirstName} {user.LastName}".Trim();
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return user;
    }

    public string HashPassword(string password) => BCrypt.Net.BCrypt.HashPassword(password);
    public bool VerifyPassword(string password, string hash) => BCrypt.Net.BCrypt.Verify(password, hash);

    public string CreateAccessToken(User user)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "secret";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var claims = new[] {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim("user_id", user.Id.ToString()),
            new Claim("phone", user.Phone)
        };
        var token = new JwtSecurityToken(
            _configuration["Jwt:Issuer"],
            _configuration["Jwt:Audience"],
            claims,
            expires: DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("Jwt:AccessTokenExpireMinutes", 60)),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string CreateRefreshToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:SecretKey"] ?? "secret"));
        var claims = new[] { new Claim("user_id", user.Id.ToString()), new Claim("type", "refresh") };
        var token = new JwtSecurityToken(_configuration["Jwt:Issuer"], _configuration["Jwt:Audience"], claims, expires: DateTime.UtcNow.AddDays(7), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateUniqueReferralCode() => Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
}