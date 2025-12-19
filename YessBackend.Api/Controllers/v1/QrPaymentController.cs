using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using YessBackend.Infrastructure.Data;
using YessBackend.Domain.Entities;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;

namespace YessBackend.Api.Controllers.v1;

[Authorize]
[ApiController]
[Route("api/v1/qr")] 
public class QrPaymentController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;
    private readonly ILogger<QrPaymentController> _logger;

    public QrPaymentController(ApplicationDbContext context, IHttpClientFactory httpClientFactory, ILogger<QrPaymentController> logger)
    {
        _context = context;
        _httpClient = httpClientFactory.CreateClient();
        _logger = logger;
    }

    [HttpPost("pay")]
    public async Task<IActionResult> Pay([FromBody] QrPayRequest request)
    {
        try
        {
            _logger.LogInformation("QR Payment attempt for merchant: {MerchantId}", request.merchant_id);

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? User.FindFirst("user_id")?.Value;

            if (!int.TryParse(userIdClaim, out int userId))
                return Unauthorized(new { detail = "Invalid token" });

            var partner = await _context.Partners.FirstOrDefaultAsync(p => p.ElqrMerchantId == request.merchant_id);
            if (partner == null) return NotFound(new { detail = "Partner not found in database" });

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null) return BadRequest(new { detail = "Wallet not found for this user" });

            decimal maxDiscount = request.amount * (partner.MaxDiscountPercent / 100m);
            decimal discountApplied = Math.Min(wallet.YescoinBalance, maxDiscount);
            decimal finalAmount = request.amount - discountApplied;

            var paymentData = new {
                user_id = userId,
                amount = (double)finalAmount,
                partner_account_id = partner.FinikAccountId?.ToString() ?? ""
            };

            var jsonPayload = JsonSerializer.Serialize(paymentData);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync("http://payment-service:8000/create/", content);

            if (!response.IsSuccessStatusCode) {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError("Django payment-service error: {Error}", err);
                return StatusCode(500, new { detail = "Payment service error", raw = err });
            }

            if (discountApplied > 0) {
                wallet.YescoinBalance -= discountApplied;
                _context.Wallets.Update(wallet);
                await _context.SaveChangesAsync();
            }

            return Ok(new {
                success = true,
                amount_charged = finalAmount,
                discount_applied = discountApplied,
                new_balance = wallet.YescoinBalance,
                partner_name = partner.Name
            });
        }
        catch (Exception ex) {
            _logger.LogError(ex, "Fatal error in QrPaymentController");
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}

public class QrPayRequest {
    public string merchant_id { get; set; } = string.Empty;
    public decimal amount { get; set; }
}
