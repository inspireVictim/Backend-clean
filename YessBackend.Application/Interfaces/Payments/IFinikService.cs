using YessBackend.Application.DTOs.FinikPayment;

namespace YessBackend.Application.Services;

public interface IFinikService
{
    Task<FinikPaymentResponseDto> CreatePaymentAsync(
        int orderId,
        decimal amount,
        string? description = null,
        string? successUrl = null,
        string? cancelUrl = null);

    Task<FinikWebhookDto> GetPaymentStatusAsync(string paymentId);

    bool VerifyWebhookSignature(string payload, string signature);

    Task<bool> ProcessWebhookAsync(FinikWebhookDto webhook);
}
