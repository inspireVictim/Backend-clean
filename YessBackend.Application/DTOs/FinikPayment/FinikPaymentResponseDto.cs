using System.Text.Json.Serialization;

namespace YessBackend.Application.DTOs.FinikPayment;

public class FinikPaymentResponseDto
{
    [JsonPropertyName("payment_id")]
    public string PaymentId { get; set; } = string.Empty;

    [JsonPropertyName("payment_url")]
    public string PaymentUrl { get; set; } = string.Empty;

    [JsonPropertyName("order_id")]
    public int OrderId { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "pending";

    [JsonPropertyName("message")]
    public string? Message { get; set; }
}
