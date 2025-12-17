using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YessBackend.Application.DTOs.FinikPayment;
using YessBackend.Application.Interfaces.Payments;
using YessBackend.Application.Services;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Webhook handler for Finik Acquiring API
/// Обрабатывает webhooks от Finik и данные от Django Payment Service
/// </summary>
[ApiController]
[Route("api/v1/webhooks/finik")]
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

    /// <summary>
    /// Receives webhook notifications from Finik (через Django Payment Service).
    /// Django проверяет RSA-подпись, затем отправляет данные сюда.
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

            _logger.LogInformation(
                "Finik Webhook received from Django: Status={Status}, Amount={Amount}, UserId={UserId}, PaymentId={PaymentId}, Currency={Currency}, TransactionId={TransactionId}",
                webhook.Status, webhook.Amount, webhook.UserId, webhook.PaymentId, webhook.Currency, webhook.TransactionId);

            // Обрабатываем базовый webhook
            await _paymentService.ProcessWebhookAsync(webhook);

            // Если это успешный платеж от Django с валютой "Yescoin", пополняем YescoinBalance
            if (webhook.Status == "SUCCEEDED" && 
                !string.IsNullOrEmpty(webhook.UserId) && 
                !string.IsNullOrEmpty(webhook.PaymentId) &&
                webhook.Currency == "Yescoin")
            {
                _logger.LogInformation(
                    "Processing Yescoin top-up: UserId={UserId}, Amount={Amount}, PaymentId={PaymentId}",
                    webhook.UserId, webhook.Amount, webhook.PaymentId);

                var (success, message, transaction) = await _walletService.TopUpYescoinBalanceAsync(
                    webhook.UserId,
                    webhook.Amount,
                    webhook.PaymentId,
                    webhook.TransactionId);

                if (success)
                {
                    _logger.LogInformation(
                        "YescoinBalance updated successfully: UserId={UserId}, Amount={Amount}, TransactionId={TransactionId}",
                        webhook.UserId, webhook.Amount, transaction?.Id);
                }
                else
                {
                    _logger.LogError(
                        "Failed to update YescoinBalance: UserId={UserId}, Amount={Amount}, PaymentId={PaymentId}, Error={Message}",
                        webhook.UserId, webhook.Amount, webhook.PaymentId, message);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Webhook skipped for Yescoin top-up: Status={Status}, Currency={Currency}, HasUserId={HasUserId}, HasPaymentId={HasPaymentId}",
                    webhook.Status, webhook.Currency, !string.IsNullOrEmpty(webhook.UserId), !string.IsNullOrEmpty(webhook.PaymentId));
            }

            _logger.LogInformation(
                "Finik webhook processed successfully. TransactionId={TransactionId}",
                webhook.TransactionId);

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
