using System.Text.Json.Serialization;

namespace YessBackend.Application.DTOs.Partner;

/// <summary>
/// DTO для ответа с данными партнера
/// Соответствует PartnerResponse из Python API
/// </summary>
public class PartnerResponseDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
    
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("category")]
    public string? Category { get; set; }
    
    [JsonPropertyName("city_id")]
    public int? CityId { get; set; }
    
    [JsonPropertyName("logo_url")]
    public string? LogoUrl { get; set; }
    
    [JsonPropertyName("cover_image_url")]
    public string? CoverImageUrl { get; set; }
    
    [JsonPropertyName("qr_code_url")]
    public string? QrCodeUrl { get; set; }
    
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }
    
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
    [JsonPropertyName("website")]
    public string? Website { get; set; }
    
    [JsonPropertyName("cashback_rate")]
    public decimal CashbackRate { get; set; }
    
    [JsonPropertyName("max_discount_percent")]
    public decimal MaxDiscountPercent { get; set; }
    
    [JsonPropertyName("latitude")]
    public double? Latitude { get; set; }
    
    [JsonPropertyName("longitude")]
    public double? Longitude { get; set; }
    
    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }
    
    [JsonPropertyName("is_verified")]
    public bool IsVerified { get; set; }
    
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}
