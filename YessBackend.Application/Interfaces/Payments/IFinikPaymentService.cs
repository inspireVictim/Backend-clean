using YessBackend.Application.DTOs.FinikPayment;

namespace YessBackend.Application.Interfaces.Payments;

public interface IFinikPaymentService
{
    /// <summary>
    /// Создает платеж через Finik Acquiring API
    /// </summary>
    Task<FinikCreatePaymentResponseDto> CreatePaymentAsync(FinikCreatePaymentRequestDto request);

    /// <summary>
    /// Обрабатывает webhook от Finik
    /// </summary>
    Task<bool> ProcessWebhookAsync(FinikWebhookDto webhook);
}
