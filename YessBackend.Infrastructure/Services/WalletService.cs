using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using YessBackend.Application.DTOs.Wallet;
using YessBackend.Application.Services;
using YessBackend.Domain.Entities;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Infrastructure.Services;

/// <summary>
/// Сервис кошелька
/// Реализует логику из Python WalletService
/// </summary>
public class WalletService : IWalletService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<WalletService> _logger;

    public WalletService(
        ApplicationDbContext context,
        IConfiguration configuration,
        ILogger<WalletService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Wallet?> GetWalletByUserIdAsync(int userId)
    {
        return await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId);
    }

    public async Task<decimal> GetBalanceAsync(int userId)
    {
        var wallet = await GetWalletByUserIdAsync(userId);
        return wallet?.Balance ?? 0.0m;
    }

    public async Task<decimal> GetYescoinBalanceAsync(int userId)
    {
        var wallet = await GetWalletByUserIdAsync(userId);
        return wallet?.YescoinBalance ?? 0.0m;
    }

    public async Task<List<Transaction>> GetUserTransactionsAsync(int userId, int limit = 50, int offset = 0)
    {
        return await _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Transaction> CreateTransactionAsync(
        int userId,
        string type,
        decimal amount,
        int? partnerId = null,
        int? orderId = null,
        string? description = null)
    {
        var wallet = await GetWalletByUserIdAsync(userId);
        if (wallet == null)
        {
            throw new InvalidOperationException("Кошелек не найден");
        }

        var balanceBefore = wallet.Balance;
        var balanceAfter = balanceBefore;

        // Обновляем баланс в зависимости от типа транзакции
        if (type == "topup" || type == "bonus")
        {
            balanceAfter = balanceBefore + amount;
            wallet.TotalEarned += amount;
        }
        else if (type == "payment" || type == "withdrawal")
        {
            if (balanceBefore < amount)
            {
                throw new InvalidOperationException("Недостаточно средств");
            }
            balanceAfter = balanceBefore - amount;
            wallet.TotalSpent += amount;
        }

        wallet.Balance = balanceAfter;
        wallet.LastUpdated = DateTime.UtcNow;

        var transaction = new Transaction
        {
            UserId = userId,
            PartnerId = partnerId,
            Type = type,
            Amount = amount,
            Status = "completed",
            BalanceBefore = balanceBefore,
            BalanceAfter = balanceAfter,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync(); // Сохраняем чтобы получить transaction.Id

        // Если это транзакция связанная с заказом, обновляем Order.TransactionId
        if (orderId.HasValue)
        {
            var order = await _context.Orders.FindAsync(orderId.Value);
            if (order != null)
            {
                order.TransactionId = transaction.Id;
                await _context.SaveChangesAsync();
            }
        }

        return transaction;
    }

    public async Task<List<Transaction>> GetTransactionHistoryAsync(int userId)
    {
        return await _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<WalletSyncResponseDto> SyncWalletAsync(WalletSyncRequestDto request)
    {
        var wallet = await GetWalletByUserIdAsync(request.UserId);
        var hasChanges = true; // Упрощенная версия, в реальности нужно сравнивать с последней синхронизацией

        if (wallet == null)
        {
            // Создаем кошелек если его нет
            wallet = new Wallet
            {
                UserId = request.UserId,
                Balance = 0.0m,
                YescoinBalance = 0.0m,
                TotalEarned = 0.0m,
                TotalSpent = 0.0m,
                LastUpdated = DateTime.UtcNow
            };
            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();
            hasChanges = false;
        }

        return new WalletSyncResponseDto
        {
            Success = true,
            YescoinBalance = wallet.YescoinBalance,
            LastUpdated = wallet.LastUpdated,
            HasChanges = hasChanges
        };
    }

    public async Task<TopUpResponseDto> TopUpWalletAsync(TopUpRequestDto request)
    {
        // Проверка существования пользователя
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId);

        if (user == null)
        {
            throw new InvalidOperationException("Пользователь не найден");
        }

        var wallet = await GetWalletByUserIdAsync(request.UserId);
        if (wallet == null)
        {
            throw new InvalidOperationException("Кошелек не найден");
        }

        // Создаем транзакцию
        var transaction = new Transaction
        {
            UserId = request.UserId,
            Type = "topup",
            Amount = request.Amount,
            BalanceBefore = wallet.Balance,
            Status = "pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        // Генерируем payment URL и QR code
        var paymentUrl = $"https://pay.yess.kg/tx/{transaction.Id}";

        // TODO: Использовать QRCoder для генерации QR кода
        // Пока возвращаем заглушку
        var qrCodeData = GenerateQRCodeData(paymentUrl);

        // Обновляем транзакцию с payment URL и QR code
        transaction.PaymentUrl = paymentUrl;
        transaction.QrCodeData = qrCodeData;
        await _context.SaveChangesAsync();

        return new TopUpResponseDto
        {
            TransactionId = transaction.Id,
            PaymentUrl = paymentUrl,
            QrCodeData = qrCodeData
        };
    }

    public async Task<object> ProcessPaymentWebhookAsync(int transactionId, string status, decimal amount)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == transactionId);

        if (transaction == null)
        {
            throw new InvalidOperationException("Транзакция не найдена");
        }

        if (transaction.Status == "completed")
        {
            return new { success = true, message = "Already processed" };
        }

        if (status == "completed" && transaction.Amount == amount)
        {
            var wallet = await GetWalletByUserIdAsync(transaction.UserId);
            if (wallet == null)
            {
                throw new InvalidOperationException("Кошелек не найден");
            }

            // Обновляем баланс с учетом множителя (по умолчанию 1.0, но можно настроить)
            var topupMultiplier = _configuration.GetValue<decimal>("Wallet:TopupMultiplier", 1.0m);
            var bonusAmount = transaction.Amount * topupMultiplier;

            wallet.Balance += bonusAmount;
            wallet.LastUpdated = DateTime.UtcNow;

            // Обновляем транзакцию
            transaction.Status = "completed";
            transaction.CompletedAt = DateTime.UtcNow;
            transaction.BalanceAfter = wallet.Balance;

            await _context.SaveChangesAsync();

            return new { success = true, message = "Payment confirmed" };
        }

        return new { success = false, message = "Invalid payment" };
    }

    /// <summary>
    /// Пополнение YescoinBalance через Finik платеж
    /// Использует транзакции БД для ACID, проверяет идемпотентность, логирует все этапы
    /// </summary>
    public async Task<(bool Success, string Message, Transaction? Transaction)> TopUpYescoinBalanceAsync(
        string userId,
        decimal amount,
        string paymentId,
        string? transactionId = null)
    {
        _logger.LogInformation(
            "TopUpYescoinBalanceAsync started: UserId={UserId}, Amount={Amount}, PaymentId={PaymentId}, TransactionId={TransactionId}",
            userId, amount, paymentId, transactionId);

        // Преобразуем userId из string в int
        if (!int.TryParse(userId, out int userIdInt))
        {
            _logger.LogError("Invalid userId format: {UserId}", userId);
            return (false, $"Invalid userId format: {userId}", null);
        }

        // Используем транзакцию БД для обеспечения ACID
        using var dbTransaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Проверка идемпотентности: ищем транзакцию с таким же GatewayTransactionId (paymentId)
            var existingTransaction = await _context.Transactions
                .FirstOrDefaultAsync(t => 
                    t.GatewayTransactionId == paymentId && 
                    t.Type == "topup" && 
                    t.Status == "completed");

            if (existingTransaction != null)
            {
                _logger.LogWarning(
                    "Payment already processed (idempotency check): PaymentId={PaymentId}, TransactionId={TransactionId}, UserId={UserId}",
                    paymentId, existingTransaction.Id, userIdInt);
                
                await dbTransaction.CommitAsync();
                return (true, "Payment already processed (idempotent)", existingTransaction);
            }

            // Получаем или создаем кошелек
            var wallet = await GetWalletByUserIdAsync(userIdInt);
            if (wallet == null)
            {
                _logger.LogInformation("Wallet not found, creating new wallet for UserId={UserId}", userIdInt);
                wallet = new Wallet
                {
                    UserId = userIdInt,
                    Balance = 0.0m,
                    YescoinBalance = 0.0m,
                    TotalEarned = 0.0m,
                    TotalSpent = 0.0m,
                    LastUpdated = DateTime.UtcNow
                };
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();
                _logger.LogInformation("New wallet created for UserId={UserId}", userIdInt);
            }

            var balanceBefore = wallet.YescoinBalance;
            var balanceAfter = balanceBefore + amount;

            _logger.LogInformation(
                "Updating YescoinBalance: UserId={UserId}, BalanceBefore={BalanceBefore}, Amount={Amount}, BalanceAfter={BalanceAfter}",
                userIdInt, balanceBefore, amount, balanceAfter);

            // Обновляем YescoinBalance
            wallet.YescoinBalance = balanceAfter;
            wallet.TotalEarned += amount;
            wallet.LastUpdated = DateTime.UtcNow;

            // Создаем запись в Transactions для истории
            var transaction = new Transaction
            {
                UserId = userIdInt,
                Type = "topup",
                Amount = amount,
                Status = "completed",
                PaymentMethod = "Finik",
                GatewayTransactionId = paymentId,
                BalanceBefore = balanceBefore,
                BalanceAfter = balanceAfter,
                Description = $"Пополнение Yescoin через Finik. PaymentId: {paymentId}",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow
            };

            _context.Transactions.Add(transaction);
            await _context.SaveChangesAsync();

            // Коммитим транзакцию БД
            await dbTransaction.CommitAsync();

            _logger.LogInformation(
                "YescoinBalance updated successfully: UserId={UserId}, TransactionId={TransactionId}, Amount={Amount}, NewBalance={NewBalance}",
                userIdInt, transaction.Id, amount, balanceAfter);

            return (true, "YescoinBalance updated successfully", transaction);
        }
        catch (Exception ex)
        {
            await dbTransaction.RollbackAsync();
            _logger.LogError(ex,
                "Error updating YescoinBalance: UserId={UserId}, PaymentId={PaymentId}, Amount={Amount}",
                userIdInt, paymentId, amount);
            return (false, $"Error updating YescoinBalance: {ex.Message}", null);
        }
    }

    private string GenerateQRCodeData(string paymentUrl)
    {
        // TODO: Реализовать генерацию QR кода через QRCoder
        // Пока возвращаем заглушку
        // В реальности нужно использовать библиотеку QRCoder для генерации изображения и конвертации в base64
        // Пример: data:image/png;base64,iVBORw0KGgoAAAANS...
        
        // Заглушка: возвращаем URL как строку для QR кода
        // В production нужно использовать QRCoder
        return $"data:image/png;base64,QR_CODE_PLACEHOLDER_FOR_{paymentUrl}";
    }
}
