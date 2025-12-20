using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YessBackend.Application.DTOs.Wallet;
using YessBackend.Application.Services;
using YessBackend.Domain.Entities;
using YessBackend.Infrastructure.Data;

namespace YessBackend.Infrastructure.Services;

public class WalletService : IWalletService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WalletService> _logger;

    public WalletService(ApplicationDbContext context, ILogger<WalletService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Wallet?> GetWalletByUserIdAsync(int userId)
        => await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);

    public async Task<decimal> GetBalanceAsync(int userId)
    {
        var wallet = await GetWalletByUserIdAsync(userId);
        return wallet?.Balance ?? 0;
    }

    public async Task<decimal> GetYescoinBalanceAsync(int userId)
    {
        var wallet = await GetWalletByUserIdAsync(userId);
        return wallet?.YescoinBalance ?? 0;
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

    public async Task<List<Transaction>> GetTransactionHistoryAsync(int userId)
    {
        return await _context.Transactions
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<Transaction> CreateTransactionAsync(
        int userId, string type, decimal amount, int? partnerId = null, int? orderId = null, string? description = null)
    {
        var transaction = new Transaction
        {
            UserId = userId,
            Type = type,
            Amount = amount,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            Status = "SUCCESS",
            PartnerId = partnerId
            // OrderId убран, так как его нет в модели Transaction
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task<WalletSyncResponseDto> SyncWalletAsync(WalletSyncRequestDto request)
    {
        var wallet = await GetWalletByUserIdAsync(request.UserId);
        if (wallet == null) return new WalletSyncResponseDto();

        wallet.LastUpdated = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new WalletSyncResponseDto
        {
            YescoinBalance = wallet.YescoinBalance
        };
    }

    public async Task<TopUpResponseDto> TopUpWalletAsync(TopUpRequestDto request)
    {
        var wallet = await GetWalletByUserIdAsync(request.UserId);
        if (wallet == null) return new TopUpResponseDto();

        wallet.Balance += request.Amount;
        wallet.TotalEarned += request.Amount;
        await _context.SaveChangesAsync();

        return new TopUpResponseDto();
    }

    public async Task<object> ProcessPaymentWebhookAsync(int transactionId, string status, decimal amount)
    {
        await Task.CompletedTask;
        return new { success = true };
    }

    public async Task<(bool Success, string Message, Transaction? Transaction)> TopUpYescoinBalanceAsync(
        string userId, decimal amount, string paymentId, string? transactionId = null)
    {
        if (!int.TryParse(userId, out int uId)) return (false, "Invalid User ID", null);

        var wallet = await GetWalletByUserIdAsync(uId);
        if (wallet == null) return (false, "Wallet not found", null);

        wallet.YescoinBalance += amount;
        var trans = await CreateTransactionAsync(uId, "TOPUP", amount, null, null, $"Finik: {paymentId}");

        return (true, "Success", trans);
    }

    public async Task<bool> SpendYescoinsAsync(int userId, int partnerId, decimal amount)
    {
        if (amount <= 0) return false;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                _logger.LogWarning("SpendYescoins: Wallet not found for user {UserId}", userId);
                return false;
            }

            if (wallet.YescoinBalance < amount)
            {
                _logger.LogWarning("SpendYescoins: Insufficient balance for user {UserId}. Has: {Balance}, Need: {Amount}", userId, wallet.YescoinBalance, amount);
                return false;
            }

            wallet.YescoinBalance -= amount;
            wallet.TotalSpent += amount;
            wallet.LastUpdated = DateTime.UtcNow;

            // 2. Создаем запись транзакции
            var transRecord = new Transaction
            {
                UserId = userId,
                PartnerId = partnerId,
                Amount = amount, // Передаем положительное число, чтобы не нарушать check_positive_amount
                Type = "QR_SPEND",
                CreatedAt = DateTime.UtcNow,
                Status = "SUCCESS",
                Description = $"QR Pay: Partner {partnerId}"
            };

            _context.Transactions.Add(transRecord);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            
            _logger.LogInformation("Successfully spent {Amount} Yescoins for user {UserId}", amount, userId);
            return true;
        }
        catch (Exception ex)
        {
            try { await transaction.RollbackAsync(); } catch { }
            _logger.LogError(ex, "FATAL ERROR during QR spend for user {UserId}: {Message}", userId, ex.Message);
            return false;
        }
    }
}
