using YessBackend.Application.DTOs.FinikPayment;

namespace YessBackend.Application.Interfaces.Payments;

public interface IFinikPaymentService
{
    Task<FinikPaymentResponseDto> CreatePaymentAsync(FinikPaymentRequestDto request);

    // Получение статуса платежа
    Task<FinikWebhookDto> GetPaymentStatusAsync(string paymentId);

    // Обработка вебхука Finik
    Task<bool> ProcessWebhookAsync(FinikWebhookDto webhook);
}
