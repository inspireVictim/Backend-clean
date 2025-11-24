using YessBackend.Application.DTOs.OptimaPayment;

namespace YessBackend.Application.Services;

/// <summary>
/// Интерфейс сервиса для обработки платежей от Optima Bank
/// </summary>
public interface IOptimaPaymentService
{
    /// <summary>
    /// Проверка состояния счета абонента (команда "check")
    /// </summary>
    Task<OptimaPaymentResponseDto> CheckAccountAsync(int account, string txnId, decimal sum);
    
    /// <summary>
    /// Пополнение баланса абонента (команда "pay")
    /// </summary>
    Task<OptimaPaymentResponseDto> ProcessPaymentAsync(int account, string txnId, decimal sum, DateTime txnDate);
}

