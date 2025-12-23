using System.Text.Json.Serialization;

namespace YessBackend.Application.DTOs.FinikPayment;

/// <summary>
/// Модель webhook от Finik Acquiring API
/// </summary>
public class FinikWebhookDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("transactionId")]
    public string TransactionId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty; // "SUCCEEDED" or "FAILED"

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("net")]
    public decimal Net { get; set; }

    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public Dictionary<string, object>? Fields { get; set; }

    [JsonPropertyName("requestDate")]
    public long RequestDate { get; set; }

    [JsonPropertyName("transactionDate")]
    public long TransactionDate { get; set; }

    [JsonPropertyName("transactionType")]
    public string TransactionType { get; set; } = string.Empty; // "DEBIT" or "CREDIT"

    [JsonPropertyName("receiptNumber")]
    public string ReceiptNumber { get; set; } = string.Empty;

    // Дополнительные поля от Django Payment Service
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("payment_id")]
    public string? PaymentId { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; } // "Yescoin" для пополнения YescoinBalance
}
