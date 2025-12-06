using System.Text.Json.Serialization;

namespace YessBackend.Application.DTOs.FinikPayment;

public class FinikWebhookDto
{
    [JsonPropertyName("payment_id")]
    public string PaymentId { get; set; } = string.Empty;

    [JsonPropertyName("order_id")]
    public int? OrderId { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime? CreatedAt { get; set; }

    [JsonPropertyName("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [JsonPropertyName("paid_at")]
    public DateTime? PaidAt { get; set; }

    [JsonPropertyName("signature")]
    public string? Signature { get; set; }
}
