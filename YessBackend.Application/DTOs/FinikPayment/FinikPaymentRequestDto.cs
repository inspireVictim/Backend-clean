using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace YessBackend.Application.DTOs.FinikPayment;

public class FinikPaymentRequestDto
{
    /// <summary>
    /// ID заказа
    /// </summary>
    [Required]
    [JsonPropertyName("order_id")]
    public int OrderId { get; set; }

    /// <summary>
    /// Сумма платежа
    /// </summary>
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Сумма должна быть больше 0")]
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Описание платежа
    /// </summary>
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    /// <summary>
    /// URL для редиректа после успешной оплаты
    /// </summary>
    [JsonPropertyName("success_url")]
    public string? SuccessUrl { get; set; }

    /// <summary>
    /// URL для редиректа после отмены оплаты
    /// </summary>
    [JsonPropertyName("cancel_url")]
    public string? CancelUrl { get; set; }
}
