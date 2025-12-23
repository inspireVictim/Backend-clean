using YessBackend.Application.DTOs.AdminAuth;

namespace YessBackend.Application.Services;

/// <summary>
/// Интерфейс сервиса аутентификации администраторов
/// </summary>
public interface IAdminAuthService
{
    /// <summary>
    /// Регистрация нового администратора
    /// </summary>
    Task<AdminResponseDto> RegisterAdminAsync(AdminRegisterDto registerDto);

    /// <summary>
    /// Вход в систему (проверка данных и генерация токенов)
    /// </summary>
    Task<AdminLoginResponseDto> LoginAsync(AdminLoginDto loginDto);

    /// <summary>
    /// Получение данных администратора по ID
    /// </summary>
    Task<AdminResponseDto?> GetAdminByIdAsync(Guid adminId);
}