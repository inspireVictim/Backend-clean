using System.Text.Json.Serialization;

namespace YessBackend.Application.DTOs.Auth;

public class UpdateProfileRequestDto
{
    [JsonPropertyName("firstName")]
    public string? FirstName { get; set; }

    [JsonPropertyName("lastName")]
    public string? LastName { get; set; }
    
    // Оставляем старые имена как альтернативу для совместимости
    [JsonPropertyName("first_name")]
    public string? FirstNameAlias { set => FirstName = value; }
    
    [JsonPropertyName("last_name")]
    public string? LastNameAlias { set => LastName = value; }
}
