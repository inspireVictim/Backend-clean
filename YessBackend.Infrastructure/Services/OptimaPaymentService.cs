using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using YessBackend.Application.DTOs.OptimaPayment;
using YessBackend.Application.Enums;
using YessBackend.Application.Services;
using YessBackend.Domain.Entities;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Infrastructure.Services;

/// <summary>
/// Сервис для обработки платежей от Optima Bank
/// Реализует протокол QIWI (как пример)
/// </summary>
public class OptimaPaymentService : IOptimaPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly IWalletService _walletService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OptimaPaymentService> _logger;

    public OptimaPaymentService(
        ApplicationDbContext context,
        IWalletService walletService,
        IConfiguration configuration,
        ILogger<OptimaPaymentService> logger)
    {
        _context = context;
        _walletService = walletService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<OptimaPaymentResponseDto> CheckAccountAsync(
        int account, 
        string txnId, 
        decimal sum,
        string? ipAddress = null,
        string? userAgent = null,
        string? rawRequest = null)
    {
        _logger.LogInformation("Optima check request: Account={Account}, TxnId={TxnId}, Sum={Sum}", 
            account, txnId, sum);

        OptimaPaymentResponseDto responseDto;

        // Проверка включен ли прием платежей
        var enabled = _configuration.GetValue<bool>("OptimaPayment:Enabled", true);
        if (!enabled)
        {
            _logger.LogWarning("Optima payment processing is disabled");
            responseDto = new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.PaymentForbidden,
                Comment = "Прием платежей отключен"
            };
        }

        // Валидация суммы
        var minAmount = _configuration.GetValue<decimal>("OptimaPayment:MinAmount", 1.0m);
        var maxAmount = _configuration.GetValue<decimal>("OptimaPayment:MaxAmount", 100000.0m);

        if (sum < minAmount)
        {
            _logger.LogWarning("Amount too small: {Sum} < {MinAmount}", sum, minAmount);
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.AmountTooSmall,
                Comment = $"Минимальная сумма пополнения: {minAmount:F2}"
            };
        }

        if (sum > maxAmount)
        {
            _logger.LogWarning("Amount too large: {Sum} > {MaxAmount}", sum, maxAmount);
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.AmountTooLarge,
                Comment = $"Максимальная сумма пополнения: {maxAmount:F2}"
            };
        }

        // Проверка существования пользователя
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == account);

        if (user == null)
        {
            _logger.LogWarning("User not found: Account={Account}", account);
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.AccountNotFound,
                Comment = "Пользователь не найден"
            };
        }

        // Проверка активности пользователя
        if (!user.IsActive || user.IsBlocked)
        {
            _logger.LogWarning("User account not active: Account={Account}, IsActive={IsActive}, IsBlocked={IsBlocked}", 
                account, user.IsActive, user.IsBlocked);
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.AccountNotActive,
                Comment = "Счет абонента не активен"
            };
        }

        // Проверка существования кошелька
        var wallet = await _walletService.GetWalletByUserIdAsync(account);
        if (wallet == null)
        {
            _logger.LogWarning("Wallet not found for user: Account={Account}", account);
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.CannotCheckAccount,
                Comment = "Кошелек не найден"
            };
        }

            _logger.LogInformation("Optima check successful: Account={Account}, TxnId={TxnId}, Sum={Sum}", 
                account, txnId, sum);

            responseDto = new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.Ok,
                Comment = "OK"
            };
        }

        // Сохраняем запрос в PaymentProviderTransactions для сверки (требование QIWI OSMP)
        try
        {
            await SaveProviderTransactionAsync(
                qid: txnId,
                operationType: "check",
                account: account.ToString(),
                amount: sum,
                status: responseDto.Result == OptimaResultCode.Ok ? "success" : "error",
                errorCode: responseDto.Result != OptimaResultCode.Ok ? ((int)responseDto.Result).ToString() : null,
                errorMessage: responseDto.Comment,
                ipAddress: ipAddress,
                userAgent: userAgent,
                rawRequest: rawRequest,
                rawResponse: null
            );
        }
        catch (Exception ex)
        {
            // Логируем ошибку сохранения, но не прерываем обработку
            _logger.LogError(ex, "Failed to save PaymentProviderTransaction for check request");
        }

        return responseDto;
    }

    public async Task<OptimaPaymentResponseDto> ProcessPaymentAsync(
        int account, 
        string txnId, 
        decimal sum, 
        DateTime txnDate,
        string? ipAddress = null,
        string? userAgent = null,
        string? rawRequest = null)
    {
        _logger.LogInformation("Optima pay request: Account={Account}, TxnId={TxnId}, Sum={Sum}, TxnDate={TxnDate}", 
            account, txnId, sum, txnDate);

        // Проверка включен ли прием платежей
        var enabled = _configuration.GetValue<bool>("OptimaPayment:Enabled", true);
        if (!enabled)
        {
            _logger.LogWarning("Optima payment processing is disabled");
            var responseDto = new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.PaymentForbidden,
                Comment = "Прием платежей отключен"
            };
            
            // Сохраняем запрос даже при ошибке
            await SaveProviderTransactionAsync(
                qid: txnId,
                operationType: "pay",
                account: account.ToString(),
                amount: sum,
                status: "error",
                errorCode: ((int)responseDto.Result).ToString(),
                errorMessage: responseDto.Comment,
                ipAddress: ipAddress,
                userAgent: userAgent,
                rawRequest: rawRequest,
                rawResponse: null
            );
            
            return responseDto;
        }

        // Проверка идемпотентности - сначала проверяем PaymentProviderTransactions (требование QIWI OSMP)
        var existingProviderTransaction = await _context.PaymentProviderTransactions
            .FirstOrDefaultAsync(t => t.Qid == txnId && t.OperationType == "pay" && t.IsProcessed);

        if (existingProviderTransaction != null && existingProviderTransaction.Status == "success")
        {
            _logger.LogInformation("Duplicate payment transaction found (idempotency): TxnId={TxnId}, PaymentProviderTransactionId={Id}", 
                txnId, existingProviderTransaction.Id);

            // Если платеж уже успешно обработан - возвращаем предыдущий результат
            var internalTransactionId = existingProviderTransaction.InternalTransactionId;
            if (internalTransactionId.HasValue)
            {
                return new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txnId,
                    PrvTxn = internalTransactionId.Value.ToString(),
                    Sum = existingProviderTransaction.Amount ?? sum,
                    Result = OptimaResultCode.Ok,
                    Comment = "Платеж уже был обработан ранее"
                };
            }
        }

        // Также проверяем внутреннюю транзакцию (для обратной совместимости)
        var existingTransaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.GatewayTransactionId == txnId && t.Type == "topup" && t.Status == "completed");

        if (existingTransaction != null)
        {
            _logger.LogInformation("Duplicate transaction found: TxnId={TxnId}, Status={Status}, TransactionId={TransactionId}", 
                txnId, existingTransaction.Status, existingTransaction.Id);

            // Если транзакция уже успешно обработана - возвращаем предыдущий результат (идемпотентность)
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                PrvTxn = existingTransaction.Id.ToString(),
                Sum = existingTransaction.Amount,
                Result = OptimaResultCode.Ok,
                Comment = "Платеж уже был обработан ранее"
            };
        }

        // Валидация суммы
        var minAmount = _configuration.GetValue<decimal>("OptimaPayment:MinAmount", 1.0m);
        var maxAmount = _configuration.GetValue<decimal>("OptimaPayment:MaxAmount", 100000.0m);

        if (sum < minAmount)
        {
            _logger.LogWarning("Amount too small: {Sum} < {MinAmount}", sum, minAmount);
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.AmountTooSmall,
                Comment = $"Минимальная сумма пополнения: {minAmount:F2}"
            };
        }

        if (sum > maxAmount)
        {
            _logger.LogWarning("Amount too large: {Sum} > {MaxAmount}", sum, maxAmount);
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.AmountTooLarge,
                Comment = $"Максимальная сумма пополнения: {maxAmount:F2}"
            };
        }

        // Проверка существования пользователя
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == account);

        if (user == null)
        {
            _logger.LogWarning("User not found: Account={Account}", account);
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.AccountNotFound,
                Comment = "Пользователь не найден"
            };
        }

        // Проверка активности пользователя
        if (!user.IsActive || user.IsBlocked)
        {
            _logger.LogWarning("User account not active: Account={Account}, IsActive={IsActive}, IsBlocked={IsBlocked}", 
                account, user.IsActive, user.IsBlocked);
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.AccountNotActive,
                Comment = "Счет абонента не активен"
            };
        }

        try
        {
            // Пополнение баланса через WalletService
            var transaction = await _walletService.CreateTransactionAsync(
                userId: account,
                type: "topup",
                amount: sum,
                description: $"Пополнение через Optima Bank. TxnId: {txnId}"
            );

            // Сохраняем txn_id в GatewayTransactionId
            transaction.GatewayTransactionId = txnId;
            transaction.PaymentMethod = "optima";
            await _context.SaveChangesAsync();

            _logger.LogInformation("Optima payment successful: Account={Account}, TxnId={TxnId}, Sum={Sum}, TransactionId={TransactionId}", 
                account, txnId, sum, transaction.Id);

            var successResponse = new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                PrvTxn = transaction.Id.ToString(),
                Sum = sum,
                Result = OptimaResultCode.Ok,
                Comment = "OK"
            };

            // Сохраняем запрос в PaymentProviderTransactions для сверки
            try
            {
                await SaveProviderTransactionAsync(
                    qid: txnId,
                    operationType: "pay",
                    account: account.ToString(),
                    amount: sum,
                    txnDate: txnDate.ToString("yyyyMMddHHmmss"),
                    status: "success",
                    errorCode: null,
                    errorMessage: null,
                    paymentId: transaction.Id.ToString(),
                    paymentStatus: "completed",
                    internalTransactionId: transaction.Id,
                    ipAddress: ipAddress,
                    userAgent: userAgent,
                    rawRequest: rawRequest,
                    rawResponse: null
                );
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to save PaymentProviderTransaction for pay request");
            }

            return successResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Optima payment: Account={Account}, TxnId={TxnId}, Sum={Sum}", 
                account, txnId, sum);
            
            var errorResponse = new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.OtherError,
                Comment = $"Ошибка обработки платежа: {ex.Message}"
            };

            // Сохраняем ошибку в PaymentProviderTransactions
            try
            {
                await SaveProviderTransactionAsync(
                    qid: txnId,
                    operationType: "pay",
                    account: account.ToString(),
                    amount: sum,
                    status: "error",
                    errorCode: ((int)OptimaResultCode.OtherError).ToString(),
                    errorMessage: errorResponse.Comment,
                    ipAddress: ipAddress,
                    userAgent: userAgent,
                    rawRequest: rawRequest,
                    rawResponse: null
                );
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx, "Failed to save PaymentProviderTransaction error");
            }

            return errorResponse;
        }
    }

    /// <summary>
    /// Сохраняет транзакцию от платежного провайдера для сверки (требование QIWI OSMP)
    /// </summary>
    private async Task SaveProviderTransactionAsync(
        string qid,
        string operationType,
        string? account = null,
        decimal? amount = null,
        string? txnDate = null,
        string status = "pending",
        string? errorCode = null,
        string? errorMessage = null,
        string? accountStatus = null,
        string? accountName = null,
        decimal? accountBalance = null,
        string? paymentStatus = null,
        string? paymentId = null,
        int? internalTransactionId = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? rawRequest = null,
        string? rawResponse = null)
    {
        try
        {
            var providerTransaction = new PaymentProviderTransaction
            {
                Qid = qid,
                Provider = "optima_bank",
                OperationType = operationType,
                Account = account,
                Amount = amount,
                TxnDate = txnDate,
                Status = status,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                AccountStatus = accountStatus,
                AccountName = accountName,
                AccountBalance = accountBalance,
                PaymentStatus = paymentStatus,
                PaymentId = paymentId,
                InternalTransactionId = internalTransactionId,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                RawRequest = rawRequest,
                RawResponse = rawResponse,
                IsProcessed = status == "success",
                IsDuplicate = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.PaymentProviderTransactions.Add(providerTransaction);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Логируем, но не прерываем основную логику
            _logger.LogWarning(ex, "Failed to save PaymentProviderTransaction: Qid={Qid}", qid);
        }
    }
}

