using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YessBackend.Application.DTOs.Wallet;
using YessBackend.Application.Services;
using AutoMapper;
using System.Security.Claims;

namespace YessBackend.Api.Controllers.v1;

[ApiController]
[Route("api/v1/wallet")]
[Tags("Wallet")]
[Authorize]
public class WalletController : ControllerBase
{
    private readonly IWalletService _walletService;
    private readonly IMapper _mapper;
    private readonly ILogger<WalletController> _logger;

    public WalletController(
        IWalletService walletService,
        IMapper mapper,
        ILogger<WalletController> logger)
    {
        _walletService = walletService;
        _mapper = mapper;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<WalletResponseDto>> GetWallet()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "Неверный токен" });

            var wallet = await _walletService.GetWalletByUserIdAsync(userId.Value);
            if (wallet == null) return NotFound(new { error = "Кошелек не найден" });

            var response = _mapper.Map<WalletResponseDto>(wallet);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения кошелька");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    /// <summary>
    /// Получить баланс (ОБНОВЛЕНО: теперь balance возвращает Yescoin)
    /// GET /api/v1/wallet/balance
    /// </summary>
    [HttpGet("balance")]
    public async Task<ActionResult> GetBalance()
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "Неверный токен" });

            // Читаем оба баланса
            var moneyBalance = await _walletService.GetBalanceAsync(userId.Value);
            var yescoinBalance = await _walletService.GetYescoinBalanceAsync(userId.Value);

            // Для фронтенда отдаем YescoinBalance в поле balance, как они просили
            return Ok(new
            {
                balance = yescoinBalance, // Основной баланс для приложения
                yescoin_balance = yescoinBalance,
                fiat_balance = moneyBalance // Реальные деньги на отдельное поле
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения баланса");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<List<TransactionResponseDto>>> GetTransactions(
        [FromQuery] int limit = 50,
        [FromQuery] int offset = 0)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "Неверный токен" });

            var transactions = await _walletService.GetUserTransactionsAsync(userId.Value, limit, offset);
            var response = transactions.Select(t => _mapper.Map<TransactionResponseDto>(t)).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения транзакций");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    [HttpPost("sync")]
    public async Task<ActionResult<WalletSyncResponseDto>> SyncWallet([FromBody] WalletSyncRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "Неверный токен" });
            request.UserId = userId.Value;
            var response = await _walletService.SyncWalletAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка синхронизации кошелька");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    [HttpPost("topup")]
    public async Task<ActionResult<TopUpResponseDto>> TopUpWallet([FromBody] TopUpRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            if (userId == null) return Unauthorized(new { error = "Неверный токен" });
            request.UserId = userId.Value;
            var response = await _walletService.TopUpWalletAsync(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка пополнения кошелька");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    [HttpPost("webhook")]
    public async Task<ActionResult> PaymentWebhook(
        [FromQuery] int transaction_id,
        [FromQuery] string status,
        [FromQuery] decimal amount)
    {
        try
        {
            var result = await _walletService.ProcessPaymentWebhookAsync(transaction_id, status, amount);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка обработки webhook");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    [HttpGet("history")]
    public async Task<ActionResult<List<TransactionResponseDto>>> GetHistory([FromQuery] int user_id)
    {
        try
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId == null || currentUserId.Value != user_id) return Forbid();

            var transactions = await _walletService.GetTransactionHistoryAsync(user_id);
            var response = transactions.Select(t => _mapper.Map<TransactionResponseDto>(t)).ToList();
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка получения истории транзакций");
            return StatusCode(500, new { error = "Внутренняя ошибка сервера" });
        }
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst("user_id")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (int.TryParse(userIdClaim, out var userId)) return userId;
        return null;
    }
}
