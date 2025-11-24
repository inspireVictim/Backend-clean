using YessBackend.Application.Enums;

namespace YessBackend.Application.DTOs.OptimaPayment;

/// <summary>
/// DTO для ответа платежной системе Optima
/// </summary>
public class OptimaPaymentResponseDto
{
    /// <summary>
    /// Номер транзакции в системе Optima (передается обратно)
    /// </summary>
    public string OsmpTxnId { get; set; } = string.Empty;
    
    /// <summary>
    /// Уникальный номер операции пополнения баланса в нашей системе (только для команды "pay")
    /// </summary>
    public string? PrvTxn { get; set; }
    
    /// <summary>
    /// Сумма платежа (дробное число с точностью до сотых)
    /// </summary>
    public decimal Sum { get; set; }
    
    /// <summary>
    /// Код результата завершения запроса
    /// </summary>
    public OptimaResultCode Result { get; set; }
    
    /// <summary>
    /// Комментарий завершения операции (необязательный)
    /// </summary>
    public string? Comment { get; set; }
}

