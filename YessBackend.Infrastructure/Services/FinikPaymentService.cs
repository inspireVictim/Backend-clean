using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using YessBackend.Infrastructure.Data;
using YessBackend.Application.Interfaces.Payments;
using YessBackend.Application.DTOs.FinikPayment;

namespace YessBackend.Infrastructure.Services;

public class FinikPaymentService : IFinikPaymentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FinikPaymentService> _logger;
    private readonly HttpClient _httpClient;

    public FinikPaymentService(
        ApplicationDbContext context,
        ILogger<FinikPaymentService> logger,
        HttpClient httpClient)
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<bool> ProcessWebhookAsync(FinikWebhookDto dto)
    {
        // Выбираем ID платежа
        var searchId = !string.IsNullOrEmpty(dto.PaymentId) ? dto.PaymentId : dto.TransactionId;

        if (string.IsNullOrEmpty(searchId))
        {
            _logger.LogWarning("!!! Finik Webhook: No ID provided");
            return false;
        }

        _logger.LogInformation(">>> START FINIK WEBHOOK FOR ID: {Id}", searchId);

        try
        {
            string userIdStr = null;
            decimal originalAmount = 0;

            // 1. ПОЛУЧАЕМ ДАННЫЕ ИЗ БД (Django table)
            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                if (int.TryParse(searchId, out int numericId))
                {
                    command.CommandText = "SELECT user_id, amount FROM payments_payment WHERE id = @id";
                    var p = command.CreateParameter();
                    p.ParameterName = "@id";
                    p.Value = numericId;
                    command.Parameters.Add(p);
                }
                else
                {
                    command.CommandText = "SELECT user_id, amount FROM payments_payment WHERE payment_id = @id::uuid";
                    var p = command.CreateParameter();
                    p.ParameterName = "@id";
                    p.Value = searchId;
                    command.Parameters.Add(p);
                }

                if (command.Connection.State != ConnectionState.Open)
                    await command.Connection.OpenAsync();

                using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    userIdStr = reader.GetValue(0)?.ToString();
                    originalAmount = reader.GetDecimal(1);
                }
                else
                {
                    _logger.LogError("!!! Payment {Id} not found in database", searchId);
                    return false;
                }
            }

            // ПРОВЕРКА ПОЛЬЗОВАТЕЛЯ
            if (string.IsNullOrEmpty(userIdStr) || userIdStr == "guest_user")
            {
                _logger.LogWarning("!!! Payment {Id} has no valid user (guest_user). Aborting wallet update.", searchId);
                return false;
            }

            if (!int.TryParse(userIdStr, out int userId))
            {
                _logger.LogError("!!! Cannot parse UserId '{Raw}' to integer", userIdStr);
                return false;
            }

            // 2. ЛОГИКА МНОЖИТЕЛЕЙ (Синхронно с контроллером)
            decimal multiplier = originalAmount >= 5000 ? 10m : 5m;
            decimal yescoinBonus = originalAmount * multiplier;

            _logger.LogInformation("CALCULATION: User {User}, Amount {Am}, Multiplier {M} = {Coins} Coins",
                userId, originalAmount, multiplier, yescoinBonus);

            using var transaction = await _context.Database.BeginTransactionAsync();

            // 3. ОБНОВЛЕНИЕ КОШЕЛЬКА (UPSERT - создаст если нет)
            // Добавлена поддержка создания записи, если UserId еще не в таблице wallets
            var upsertWalletSql = @"
                INSERT INTO wallets (""UserId"", ""Balance"", ""YescoinBalance"", ""TotalEarned"", ""LastUpdated"")
                VALUES ({3}, {0}, {1}, {0}, {2})
                ON CONFLICT (""UserId"") 
                DO UPDATE SET 
                    ""Balance"" = wallets.""Balance"" + EXCLUDED.""Balance"",
                    ""YescoinBalance"" = wallets.""YescoinBalance"" + EXCLUDED.""YescoinBalance"",
                    ""TotalEarned"" = wallets.""TotalEarned"" + EXCLUDED.""Balance"",
                    ""LastUpdated"" = EXCLUDED.""LastUpdated"";";

            int walletRows = await _context.Database.ExecuteSqlRawAsync(upsertWalletSql,
                originalAmount,     // {0}
                yescoinBonus,      // {1}
                DateTime.UtcNow,    // {2}
                userId);            // {3}

            // 4. ОБНОВЛЕНИЕ СТАТУСА ПЛАТЕЖА
            if (int.TryParse(searchId, out int nId))
            {
                await _context.Database.ExecuteSqlRawAsync("UPDATE payments_payment SET status = 'SUCCESS' WHERE id = {0}", nId);
            }
            else
            {
                await _context.Database.ExecuteSqlRawAsync("UPDATE payments_payment SET status = 'SUCCESS' WHERE payment_id = {0}::uuid", searchId);
            }

            await transaction.CommitAsync();

            _logger.LogInformation(">>> FINISHED: User {User} balance updated. Added {Coin} YessCoins. Rows affected: {Count}",
                userId, yescoinBonus, walletRows);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "!!! FATAL ERROR in FinikPaymentService: {Msg}", ex.Message);
            return false;
        }
    }

    public Task<FinikCreatePaymentResponseDto> CreatePaymentAsync(FinikCreatePaymentRequestDto dto)
        => Task.FromResult(new FinikCreatePaymentResponseDto());
}