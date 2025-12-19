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
    private readonly HttpClient _httpClient; // Возвращаем поле

    public FinikPaymentService(
        ApplicationDbContext context, 
        ILogger<FinikPaymentService> logger,
        HttpClient httpClient) // Возвращаем параметр в конструктор
    {
        _context = context;
        _logger = logger;
        _httpClient = httpClient;
    }

    public async Task<bool> ProcessWebhookAsync(FinikWebhookDto dto)
    {
        if (string.IsNullOrEmpty(dto.TransactionId)) return false;

        _logger.LogInformation(">>> START FINIK WEBHOOK: {Id}", dto.TransactionId);

        try 
        {
            using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT user_id, amount FROM payments_payment WHERE payment_id = @id::uuid";
            
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@id";
            parameter.Value = dto.TransactionId;
            command.Parameters.Add(parameter);

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
                    _logger.LogError("!!! Payment {Id} not found in database", dto.TransactionId);
                    return false;
                }
            }

            if (!int.TryParse(userIdStr, out int userId)) return false;

            // ЛОГИКА X2
            decimal yescoinBonus = originalAmount * 2;
            _logger.LogInformation("Converted {Som} SOM to {Coin} YessCoins for User {User}", originalAmount, yescoinBonus, userId);

            using var transaction = await _context.Database.BeginTransactionAsync();
            
            // Используем кавычки и COALESCE на случай, если в базе NULL
            var updateWalletSql = "UPDATE wallets SET \"YescoinBalance\" = COALESCE(\"YescoinBalance\", 0) + {0}, \"LastUpdated\" = {1} WHERE \"UserId\" = {2}";
            int walletRows = await _context.Database.ExecuteSqlRawAsync(updateWalletSql, yescoinBonus, DateTime.UtcNow, userId);

            var updatePaymentSql = "UPDATE payments_payment SET status = 'SUCCESS' WHERE payment_id = {0}::uuid";
            await _context.Database.ExecuteSqlRawAsync(updatePaymentSql, dto.TransactionId);

            await transaction.CommitAsync();
            
            _logger.LogInformation(">>> FINISHED: Rows affected: {Count}. User {User} now has more YessCoins.", walletRows, userId);
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
