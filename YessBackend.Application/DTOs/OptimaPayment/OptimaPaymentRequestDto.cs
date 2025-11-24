namespace YessBackend.Application.DTOs.OptimaPayment;

/// <summary>
/// DTO для запроса от платежной системы Optima
/// </summary>
public class OptimaPaymentRequestDto
{
    /// <summary>
    /// Тип команды: "check" (проверка) или "pay" (пополнение)
    /// </summary>
    public string Command { get; set; } = string.Empty;
    
    /// <summary>
    /// Уникальный идентификатор транзакции в системе Optima (до 20 знаков)
    /// </summary>
    public string TxnId { get; set; } = string.Empty;
    
    /// <summary>
    /// Идентификатор абонента (ID пользователя в нашей системе)
    /// </summary>
    public string Account { get; set; } = string.Empty;
    
    /// <summary>
    /// Сумма платежа (дробное число с точностью до сотых, разделитель ".")
    /// </summary>
    public string Sum { get; set; } = string.Empty;
    
    /// <summary>
    /// Дата платежа в формате ГГГГММДДЧЧММСС (только для команды "pay")
    /// </summary>
    public string? TxnDate { get; set; }
}

