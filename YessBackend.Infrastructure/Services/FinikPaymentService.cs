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
        // Выбираем ID платежа (числовой ID или UUID)
        var searchId = !string.IsNullOrEmpty(dto.PaymentId) ? dto.PaymentId : dto.TransactionId;

        if (string.IsNullOrEmpty(searchId))
        {
            _logger.LogWarning("!!! Webhook received without any PaymentId or TransactionId");
            return false;
        }

        _logger.LogInformation(">>> START PROCESSING WEBHOOK. ID: {Id}", searchId);

        try
        {
            string userIdStr = null;
            decimal originalAmount = 0;

            // 1. ИЩЕМ ПЛАТЕЖ В БАЗЕ
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
            }

            // ПРОВЕРКИ
            if (string.IsNullOrEmpty(userIdStr) || userIdStr.ToLower() == "guest_user")
            {
                _logger.LogWarning("!!! PAYMENT {Id} IGNORED: User is '{UserStr}'. No wallet to update.", searchId, userIdStr ?? "null");
                return false;
            }

            if (!int.TryParse(userIdStr, out int userId))
            {
                _logger.LogError("!!! FAILED TO PARSE USER_ID: {Raw}", userIdStr);
                return false;
            }

            // 2. РАСЧЕТ МНОЖИТЕЛЯ (Коэффициенты)
            decimal multiplier = originalAmount >= 5000 ? 10m : 5m;
            decimal yescoinBonus = originalAmount * multiplier;

            _logger.LogInformation("CALCULATION: Amount {Am} * Multiplier {M} = {Res} YessCoins", originalAmount, multiplier, yescoinBonus);

            using var transaction = await _context.Database.BeginTransactionAsync();

            // 3. ОБНОВЛЕНИЕ ИЛИ СОЗДАНИЕ КОШЕЛЬКА (UPSERT)
            // Используем ON CONFLICT, чтобы если кошелька нет, он создался автоматически
            var upsertWalletSql = @"
                INSERT INTO wallets (""UserId"", ""Balance"", ""YescoinBalance"", ""TotalEarned"", ""LastUpdated"")
                VALUES ({3}, {0}, {1}, {0}, {2})
                ON CONFLICT (""UserId"") 
                DO UPDATE SET 
                    ""Balance"" = wallets.""Balance"" + EXCLUDED.""Balance"",
                    ""YescoinBalance"" = wallets.""YescoinBalance"" + EXCLUDED.""YescoinBalance"",
                    ""TotalEarned"" = wallets.""TotalEarned"" + EXCLUDED.""Balance"",
                    ""LastUpdated"" = EXCLUDED.""LastUpdated"";";

            int affectedRows = await _context.Database.ExecuteSqlRawAsync(upsertWalletSql,
                originalAmount,     // {0}
                yescoinBonus,      // {1}
                DateTime.UtcNow,    // {2}
                userId);            // {3}

            // 4. ОБНОВЛЕНИЕ СТАТУСА ПЛАТЕЖА
            string updatePaymentSql;
            if (int.TryParse(searchId, out int nId))
            {
                updatePaymentSql = "UPDATE payments_payment SET status = 'SUCCESS' WHERE id = {0}";
                await _context.Database.ExecuteSqlRawAsync(updatePaymentSql, nId);
            }
            else
            {
                updatePaymentSql = "UPDATE payments_payment SET status = 'SUCCESS' WHERE payment_id = {0}::uuid";
                await _context.Database.ExecuteSqlRawAsync(updatePaymentSql, searchId);
            }

            await transaction.CommitAsync();

            _logger.LogInformation(">>> SUCCESS: User {User} updated. Added {Coins} coins. (Rows: {Rows})", userId, yescoinBonus, affectedRows);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "!!! FATAL ERROR IN WEBHOOK: {Msg}", ex.Message);
            return false;
        }
    }

    public Task<FinikCreatePaymentResponseDto> CreatePaymentAsync(FinikCreatePaymentRequestDto dto)
        => Task.FromResult(new FinikCreatePaymentResponseDto());
}