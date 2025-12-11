using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YessBackend.Application.DTOs.FinikPayment;
using YessBackend.Application.Interfaces.Payments;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Webhook handler for Finik Acquiring API
/// </summary>
[ApiController]
[Route("api/v1/webhooks/finik")]
[Tags("Webhooks")]
public class FinikWebhookController : ControllerBase
{
    private readonly IFinikPaymentService _paymentService;
    private readonly ILogger<FinikWebhookController> _logger;

    public FinikWebhookController(
        IFinikPaymentService paymentService,
        ILogger<FinikWebhookController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    /// <summary>
    /// Receives webhook notifications from Finik.
    /// Finik does NOT send signatures — so no verification is required.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ReceiveWebhook([FromBody] FinikWebhookDto webhook)
    {
        try
        {
            if (webhook == null)
            {
                _logger.LogWarning("Finik webhook received but body was null");
                return Ok(new { success = false, error = "Empty body" });
            }

            _logger.LogInformation("Finik Webhook received: {Data}", JsonSerializer.Serialize(webhook));

            // Process webhook
            await _paymentService.ProcessWebhookAsync(webhook);

            _logger.LogInformation("Finik webhook processed successfully. TransactionId={TransactionId}", webhook.TransactionId);

            // Must ALWAYS return 200 OK or Finik will retry
            return Ok(new
            {
                success = true,
                transactionId = webhook.TransactionId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Finik webhook");

            // Still respond 200 according to Finik requirements
            return Ok(new
            {
                success = false,
                error = "Internal error"
            });
        }
    }
}
