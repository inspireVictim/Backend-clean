using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using YessBackend.Application.DTOs.FinikPayment;
using YessBackend.Application.Interfaces.Payments;

namespace YessBackend.Api.Controllers.v1;

[ApiController]
[Route("api/v1/payment/finik")]
[Tags("Payments Finik")]
public class FinikPaymentController : ControllerBase
{
    private readonly IFinikPaymentService _paymentService;

    public FinikPaymentController(IFinikPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost("create")]
    [Authorize]
    public async Task<IActionResult> CreatePayment([FromBody] FinikPaymentRequestDto request)
    {
        var result = await _paymentService.CreatePaymentAsync(request);
        return Ok(result);
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook([FromBody] FinikWebhookDto webhook)
    {
        await _paymentService.ProcessWebhookAsync(webhook);
        return Ok(new { success = true });
    }
}
