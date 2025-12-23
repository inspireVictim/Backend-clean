namespace YessBackend.Application.DTOs.AdminAuth;

/// <summary>
/// Полный ответ при успешном входе администратора
/// </summary>
public class AdminLoginResponseDto
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public AdminResponseDto Admin { get; set; } = null!;
}