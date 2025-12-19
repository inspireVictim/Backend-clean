using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using YessBackend.Application.DTOs.FinikPayment;
using YessBackend.Application.Interfaces.Payments;
using YessBackend.Application.Services;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Webhook handler for Finik Acquiring API
/// </summary>
[ApiController]
[Route("api/v1/webhooks/finik")]
[AllowAnonymous] // РАЗРЕШАЕМ ДОСТУП БЕЗ JWT ТОКЕНА
[Tags("Webhooks")]
public class FinikWebhookController : ControllerBase
{
    private readonly IFinikPaymentService _paymentService;
    private readonly IWalletService _walletService;
    private readonly ILogger<FinikWebhookController> _logger;

    public FinikWebhookController(
        IFinikPaymentService paymentService,
        IWalletService walletService,
        ILogger<FinikWebhookController> logger)
    {
        _paymentService = paymentService;
        _walletService = walletService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> ReceiveWebhook([FromBody] FinikWebhookDto webhook)
    {
        try
        {
            if (webhook == null) return Ok(new { success = false });

            _logger.LogInformation("Finik Webhook received for PaymentId: {PaymentId}", webhook.PaymentId);

            // 1. Вызываем наш сервис (который мы обновили выше), чтобы обновить БД и баланс
            // ВАЖНО: Убедитесь, что ваш IFinikPaymentService внутри вызывает WebhookService.ProcessPaymentCallbackAsync
            await _paymentService.ProcessWebhookAsync(webhook);

            return Ok(new { success = true, transactionId = webhook.TransactionId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Finik webhook");
            return Ok(new { success = false });
        }
    }
}
