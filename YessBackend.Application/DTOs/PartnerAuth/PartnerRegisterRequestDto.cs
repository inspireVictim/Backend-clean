using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace YessBackend.Application.DTOs.PartnerAuth;

/// <summary>
/// DTO для запроса регистрации партнера
/// </summary>
public class PartnerRegisterRequestDto
{
    // Обязательные поля
    [JsonPropertyName("name")]
    [Required(ErrorMessage = "Название партнера обязательно")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    [Required(ErrorMessage = "Категория обязательна")]
    public string Category { get; set; } = string.Empty;

    /**/

    [JsonPropertyName("phone")]
    [Required(ErrorMessage = "Телефон обязателен")]
    [Phone(ErrorMessage = "Неверный формат телефона")]
    public string Phone { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    [Required(ErrorMessage = "Пароль обязателен")]
    [MinLength(8, ErrorMessage = "Пароль должен содержать минимум 8 символов")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    [Required(ErrorMessage = "Описание обязательно")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("logo_url")]
    [Required(ErrorMessage = "Логотип обязателен")]
    [Url(ErrorMessage = "Неверный формат URL для логотипа")]
    public string LogoUrl { get; set; } = string.Empty;

    [JsonPropertyName("cover_image_url")]
    [Required(ErrorMessage = "Обложка обязательна")]
    [Url(ErrorMessage = "Неверный формат URL для обложки")]
    public string CoverImageUrl { get; set; } = string.Empty;

    [JsonPropertyName("city_id")]
    [Required(ErrorMessage = "Город обязателен")]
    public int CityId { get; set; }

    [JsonPropertyName("max_discount_percent")]
    [Required(ErrorMessage = "Максимальная скидка обязательна")]
    [Range(0, 100, ErrorMessage = "Максимальная скидка должна быть от 0 до 100")]
    public decimal MaxDiscountPercent { get; set; }

    [JsonPropertyName("address")]
    [Required(ErrorMessage = "Адрес обязателен")]
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("two_gis_url")]
    [Required(ErrorMessage = "Ссылка на 2GIS обязательна")]
    [Url(ErrorMessage = "Неверный формат URL для ссылки на 2GIS")]
    public string TwoGisUrl { get; set; } = string.Empty;

    // Необязательные поля
    [JsonPropertyName("email")]
    [EmailAddress(ErrorMessage = "Неверный формат email")]
    public string? Email { get; set; }

    [JsonPropertyName("cashback_rate")]
    [Range(0, 100, ErrorMessage = "Кэшбек должен быть от 0 до 100")]
    public decimal? CashbackRate { get; set; }

    [JsonPropertyName("website")]
    [Url(ErrorMessage = "Неверный формат URL для веб-сайта")]
    public string? Website { get; set; }
}