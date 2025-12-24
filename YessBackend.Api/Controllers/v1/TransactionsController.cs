using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YessBackend.Infrastructure.Data;
using YessBackend.Application.DTOs;
using System.Data;

namespace YessBackend.Api.Controllers.v1;

[ApiController]
[Route("api/v1/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<TransactionsController> _logger;

    public TransactionsController(ApplicationDbContext context, ILogger<TransactionsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("history/{userId}")]
    public async Task<IActionResult> GetUserHistory(int userId)
    {
        _logger.LogInformation("Fetching transaction history for user: {UserId}", userId);

        try
        {
            // Поскольку таблица payments_payment может быть не описана в EF моделях (так как она от Django),
            // мы используем сырой SQL запрос через Dapper или напрямую через контекст.

            var transactions = new List<TransactionHistoryDto>();

            using (var command = _context.Database.GetDbConnection().CreateCommand())
            {
                // Выбираем данные из таблицы платежей Django
                command.CommandText = @"
                    SELECT 
                        id::text, 
                        amount, 
                        status, 
                        payment_method, 
                        created_at 
                    FROM payments_payment 
                    WHERE user_id = @userId 
                    ORDER BY created_at DESC";

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@userId";
                parameter.Value = userId;
                command.Parameters.Add(parameter);

                if (command.Connection.State != ConnectionState.Open)
                    await command.Connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        transactions.Add(new TransactionHistoryDto
                        {
                            Id = reader.GetString(0),
                            Amount = reader.GetDecimal(1),
                            Status = reader.GetString(2),
                            PaymentMethod = reader.IsDBNull(3) ? "N/A" : reader.GetString(3),
                            CreatedAt = reader.GetDateTime(4),
                            Currency = "SOM" // Или возьмите из базы, если есть колонка
                        });
                    }
                }
            }

            if (transactions.Count == 0)
            {
                return Ok(new { message = "No transactions found", data = new List<object>() });
            }

            return Ok(new { success = true, data = transactions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching history for user {UserId}", userId);
            return StatusCode(500, "Internal server error while fetching history");
        }
    }
}