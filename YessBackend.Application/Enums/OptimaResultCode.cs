namespace YessBackend.Application.Enums;

/// <summary>
/// Коды завершения операций для платежной системы Optima
/// Соответствует протоколу QIWI (как пример)
/// </summary>
public enum OptimaResultCode
{
    /// <summary>
    /// Операция успешно выполнена
    /// </summary>
    Ok = 0,
    
    /// <summary>
    /// Временная ошибка. Повторите запрос позже
    /// </summary>
    TemporaryError = 1,
    
    /// <summary>
    /// Неверный формат идентификатора абонента
    /// </summary>
    InvalidAccountFormat = 4,
    
    /// <summary>
    /// Идентификатор абонента не найден (Ошиблись номером)
    /// </summary>
    AccountNotFound = 5,
    
    /// <summary>
    /// Прием платежа запрещен поставщиком
    /// </summary>
    PaymentForbidden = 7,
    
    /// <summary>
    /// Счет абонента не активен
    /// </summary>
    AccountNotActive = 79,
    
    /// <summary>
    /// Проведение платежа не окончено
    /// </summary>
    PaymentNotCompleted = 90,
    
    /// <summary>
    /// Сумма слишком мала
    /// </summary>
    AmountTooSmall = 241,
    
    /// <summary>
    /// Сумма слишком велика
    /// </summary>
    AmountTooLarge = 242,
    
    /// <summary>
    /// Невозможно проверить состояние счета
    /// </summary>
    CannotCheckAccount = 243,
    
    /// <summary>
    /// Другая ошибка поставщика
    /// </summary>
    OtherError = 300
}

