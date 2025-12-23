using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using YessBackend.Application.DTOs.AdminAuth;
using YessBackend.Application.Services;
using YessBackend.Domain.Entities;
using YessBackend.Infrastructure.Data; // Укажите путь к вашему DbContext

namespace YessBackend.Infrastructure.Services;

public class AdminAuthService : IAdminAuthService
{
    private readonly ApplicationDbContext _context; // Ваш класс контекста БД
    private readonly IConfiguration _configuration;

    public AdminAuthService(ApplicationDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<AdminLoginResponseDto> LoginAsync(AdminLoginDto loginDto)
    {
        // 1. Поиск админа по Username или Email
        var admin = await _context.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == loginDto.Username || u.Email == loginDto.Username);

        // 2. Проверка существования и пароля (BCrypt)
        if (admin == null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, admin.PasswordHash))
        {
            throw new InvalidOperationException("Неверное имя пользователя или пароль");
        }

        if (!admin.IsActive)
        {
            throw new InvalidOperationException("Аккаунт администратора деактивирован");
        }

        // 3. Генерация токенов
        var accessToken = CreateToken(admin, isAccessToken: true);
        var refreshToken = CreateToken(admin, isAccessToken: false);

        return new AdminLoginResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresIn = 3600,
            Admin = MapToResponseDto(admin)
        };
    }

    public async Task<AdminResponseDto> RegisterAdminAsync(AdminRegisterDto registerDto)
    {
        // Проверка на уникальность
        bool exists = await _context.AdminUsers
            .AnyAsync(u => u.Email == registerDto.Email || u.Username == registerDto.Username);

        if (exists)
            throw new InvalidOperationException("Администратор с такими данными уже существует");

        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Username = registerDto.Username,
            Email = registerDto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(registerDto.Password),
            Role = registerDto.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.AdminUsers.Add(admin);
        await _context.SaveChangesAsync();

        return MapToResponseDto(admin);
    }

    public async Task<AdminResponseDto?> GetAdminByIdAsync(Guid adminId)
    {
        var admin = await _context.AdminUsers.FindAsync(adminId);
        return admin == null ? null : MapToResponseDto(admin);
    }

    private string CreateToken(AdminUser admin, bool isAccessToken)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new Claim(ClaimTypes.Name, admin.Username),
            new Claim(ClaimTypes.Email, admin.Email),
            new Claim(ClaimTypes.Role, admin.Role)
        };

        var keyString = _configuration["Jwt:Key"] ?? "SUPER_SECRET_KEY_1234567890123456";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expires = isAccessToken ? DateTime.UtcNow.AddHours(1) : DateTime.UtcNow.AddDays(7);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private AdminResponseDto MapToResponseDto(AdminUser admin)
    {
        return new AdminResponseDto
        {
            Id = admin.Id,
            Username = admin.Username,
            Email = admin.Email,
            Role = admin.Role,
            IsActive = admin.IsActive,
            CreatedAt = admin.CreatedAt,
            UpdatedAt = admin.UpdatedAt
        };
    }
}