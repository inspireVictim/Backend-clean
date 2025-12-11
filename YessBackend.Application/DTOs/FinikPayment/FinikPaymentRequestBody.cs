using System.Text.Json.Serialization;

namespace YessBackend.Application.DTOs.FinikPayment;

public class FinikPaymentRequestBody
{
    [JsonPropertyName("Amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("CardType")]
    public string CardType { get; set; } = "FINIK_QR";

    [JsonPropertyName("PaymentId")]
    public string PaymentId { get; set; } = string.Empty;

    [JsonPropertyName("RedirectUrl")]
    public string RedirectUrl { get; set; } = string.Empty;

    [JsonPropertyName("Data")]
    public FinikPaymentData Data { get; set; } = new();
}

public class FinikPaymentData
{
    // строго в алфавитном порядке
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("endDate")]
    public long? EndDate { get; set; }

    [JsonPropertyName("merchantCategoryCode")]
    public string MerchantCategoryCode { get; set; } = string.Empty;

    [JsonPropertyName("name_en")]
    public string NameEn { get; set; } = string.Empty;

    [JsonPropertyName("startDate")]
    public long? StartDate { get; set; }

    [JsonPropertyName("webhookUrl")]
    public string? WebhookUrl { get; set; }
}
