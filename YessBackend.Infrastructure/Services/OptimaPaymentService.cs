using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
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

    public async Task<OptimaPaymentResponseDto> CheckAccountAsync(int account, string txnId, decimal sum)
    {
        _logger.LogInformation("Optima check request: Account={Account}, TxnId={TxnId}, Sum={Sum}", 
            account, txnId, sum);

        // Проверка включен ли прием платежей
        var enabled = _configuration.GetValue<bool>("OptimaPayment:Enabled", true);
        if (!enabled)
        {
            _logger.LogWarning("Optima payment processing is disabled");
            return new OptimaPaymentResponseDto
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

        return new OptimaPaymentResponseDto
        {
            OsmpTxnId = txnId,
            Sum = sum,
            Result = OptimaResultCode.Ok,
            Comment = "OK"
        };
    }

    public async Task<OptimaPaymentResponseDto> ProcessPaymentAsync(int account, string txnId, decimal sum, DateTime txnDate)
    {
        _logger.LogInformation("Optima pay request: Account={Account}, TxnId={TxnId}, Sum={Sum}, TxnDate={TxnDate}", 
            account, txnId, sum, txnDate);

        // Проверка включен ли прием платежей
        var enabled = _configuration.GetValue<bool>("OptimaPayment:Enabled", true);
        if (!enabled)
        {
            _logger.LogWarning("Optima payment processing is disabled");
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.PaymentForbidden,
                Comment = "Прием платежей отключен"
            };
        }

        // Проверка идемпотентности - ищем существующую транзакцию с таким txn_id
        var existingTransaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.GatewayTransactionId == txnId && t.Type == "topup");

        if (existingTransaction != null)
        {
            _logger.LogInformation("Duplicate transaction found: TxnId={TxnId}, Status={Status}, TransactionId={TransactionId}", 
                txnId, existingTransaction.Status, existingTransaction.Id);

            // Если транзакция уже успешно обработана - возвращаем предыдущий результат
            if (existingTransaction.Status == "completed")
            {
                return new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txnId,
                    PrvTxn = existingTransaction.Id.ToString(),
                    Sum = existingTransaction.Amount,
                    Result = OptimaResultCode.Ok,
                    Comment = "Платеж уже был обработан ранее"
                };
            }

            // Если транзакция в статусе pending или failed - можно повторить
            // Удаляем старую транзакцию и создаем новую
            _context.Transactions.Remove(existingTransaction);
            await _context.SaveChangesAsync();
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

            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                PrvTxn = transaction.Id.ToString(),
                Sum = sum,
                Result = OptimaResultCode.Ok,
                Comment = "OK"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Optima payment: Account={Account}, TxnId={TxnId}, Sum={Sum}", 
                account, txnId, sum);
            
            return new OptimaPaymentResponseDto
            {
                OsmpTxnId = txnId,
                Sum = sum,
                Result = OptimaResultCode.OtherError,
                Comment = $"Ошибка обработки платежа: {ex.Message}"
            };
        }
    }
}

