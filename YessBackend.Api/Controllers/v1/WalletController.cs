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
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var wallet = await _walletService.GetWalletByUserIdAsync(userId.Value);
        if (wallet == null) return NotFound();

        return Ok(_mapper.Map<WalletResponseDto>(wallet));
    }

    [HttpGet("balance")]
    public async Task<ActionResult> GetBalance()
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var moneyBalance = await _walletService.GetBalanceAsync(userId.Value);
        var yescoinBalance = await _walletService.GetYescoinBalanceAsync(userId.Value);

        return Ok(new
        {
            balance = yescoinBalance,
            yescoin_balance = yescoinBalance,
            fiat_balance = moneyBalance
        });
    }

    [HttpPost("spend")]
    public async Task<ActionResult> SpendCoins([FromBody] SpendCoinsRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var success = await _walletService.SpendYescoinsAsync(userId.Value, request.PartnerId, request.Amount);
        if (!success) return BadRequest(new { error = "Ошибка списания или недостаточно средств" });

        return Ok(new { message = "Успешно", spent = request.Amount });
    }

    [HttpGet("transactions")]
    public async Task<ActionResult<List<TransactionResponseDto>>> GetTransactions(
        [FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();

        var transactions = await _walletService.GetUserTransactionsAsync(userId.Value, limit, offset);
        return Ok(transactions.Select(t => _mapper.Map<TransactionResponseDto>(t)).ToList());
    }

    [HttpPost("sync")]
    public async Task<ActionResult<WalletSyncResponseDto>> SyncWallet([FromBody] WalletSyncRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Unauthorized();
        request.UserId = userId.Value;
        return Ok(await _walletService.SyncWalletAsync(request));
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst("user_id")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return int.TryParse(claim, out var id) ? id : null;
    }
}

public class SpendCoinsRequestDto 
{
    public int PartnerId { get; set; }
    public decimal Amount { get; set; }
}
