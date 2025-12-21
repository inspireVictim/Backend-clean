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
        // Берем ID платежа из того поля, которое заполнено (78 или UUID)
        var searchId = !string.IsNullOrEmpty(dto.PaymentId) ? dto.PaymentId : dto.TransactionId;

        if (string.IsNullOrEmpty(searchId)) return false;

        _logger.LogInformation(">>> START FINIK WEBHOOK FOR ID: {Id}", searchId);

        try
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();

            // Ищем по колонке 'id' (число), так как в базе Django это первичный ключ
            if (int.TryParse(searchId, out int numericId)) {
                command.CommandText = "SELECT user_id, amount FROM payments_payment WHERE id = @id";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@id";
                parameter.Value = numericId;
                command.Parameters.Add(parameter);
            } else {
                // Если пришел UUID
                command.CommandText = "SELECT user_id, amount FROM payments_payment WHERE payment_id = @id::uuid";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@id";
                parameter.Value = searchId;
                command.Parameters.Add(parameter);
            }

            if (command.Connection.State != ConnectionState.Open)
                await command.Connection.OpenAsync();

            string userIdStr = null;
            decimal originalAmount = 0;

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    userIdStr = reader.GetValue(0).ToString();
                    originalAmount = reader.GetDecimal(1);
                }
                else
                {
                    _logger.LogError("!!! Payment {Id} not found in database", searchId);
                    return false;
                }
            }

            if (!int.TryParse(userIdStr, out int userId)) return false;

            // ЛОГИКА Коэфицентов: рассчитываем бонусы
            decimal multiiplyer;

            if (originalAmount >= 5000)
            {
                multiiplyer = 5; 
            }
            else if (originalAmount >= 4000)
            {
                multiiplyer = 4; 
            }
            else if (originalAmount >= 3000)
            {
                multiiplyer = 3; 
            }
            else if (originalAmount >= 500)
            {
                multiiplyer = 2; 
            }
            else
            {
                multiiplyer = 1; 
            }

            decimal yescoinBonus = originalAmount * multiiplyer;
            _logger.LogInformation("SUCCESS: Processing User {User}. Adding {Som} SOM and {Coin} YessCoins", userId, originalAmount, yescoinBonus);

            using var transaction = await _context.Database.BeginTransactionAsync();

            // ОБНОВЛЕНИЕ КОШЕЛЬКА: Пополняем основной баланс, бонусы и статистику
            var updateWalletSql = @"
                UPDATE wallets 
                SET ""Balance"" = COALESCE(""Balance"", 0) + {0}, 
                    ""YescoinBalance"" = COALESCE(""YescoinBalance"", 0) + {1}, 
                    ""TotalEarned"" = COALESCE(""TotalEarned"", 0) + {0},
                    ""LastUpdated"" = {2} 
                WHERE ""UserId"" = {3}";

            int walletRows = await _context.Database.ExecuteSqlRawAsync(updateWalletSql, originalAmount, yescoinBonus, DateTime.UtcNow, userId);

            // ОБНОВЛЕНИЕ ПЛАТЕЖА: Меняем статус на SUCCESS
            string updatePaymentSql;
            if (int.TryParse(searchId, out int nId)) {
                updatePaymentSql = "UPDATE payments_payment SET status = 'SUCCESS' WHERE id = {0}";
                await _context.Database.ExecuteSqlRawAsync(updatePaymentSql, nId);
            } else {
                updatePaymentSql = "UPDATE payments_payment SET status = 'SUCCESS' WHERE payment_id = {0}::uuid";
                await _context.Database.ExecuteSqlRawAsync(updatePaymentSql, searchId);
            }

            await transaction.CommitAsync();

            _logger.LogInformation(">>> FINISHED: Balance and Bonuses updated for User {User}. Rows affected: {Count}", userId, walletRows);
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

