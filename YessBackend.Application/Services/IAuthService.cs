using YessBackend.Application.DTOs.Auth;
using YessBackend.Domain.Entities;
using YessBackend.Application.DTOs.Auth;
using YessBackend.Domain.Entities;

namespace YessBackend.Application.Services;

public interface IAuthService
{
    Task<User> RegisterUserAsync(UserRegisterDto userDto);
    Task<TokenResponseDto> LoginAsync(UserLoginDto loginDto);
    Task<User?> GetUserByPhoneAsync(string phone);
    Task<User?> GetUserByIdAsync(int userId);
    Task<string> SendVerificationCodeAsync(string phoneNumber);
    Task<User> VerifyCodeAndRegisterAsync(VerifyCodeAndRegisterRequestDto requestDto);
    Task<ReferralStatsResponseDto> GetReferralStatsAsync(int userId);

    // Метод для обновления профиля
    Task<User?> UpdateUserAsync(int userId, UpdateProfileRequestDto dto);

    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
    string CreateAccessToken(User user);
    string CreateRefreshToken(User user);
}


