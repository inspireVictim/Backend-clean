using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using YessBackend.Application.Services;

namespace YessBackend.Api.Controllers.v1;

[ApiController]
[Route("api/v1/webhooks")]
[Tags("Webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhookService;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        IWebhookService webhookService,
        ILogger<WebhooksController> logger)
    {
        _webhookService = webhookService;
        _logger = logger;
    }

    [HttpPost("payment/callback")]
    public async Task<ActionResult> PaymentCallback([FromBody] JsonElement payload)
    {
        try
        {
            await _webhookService.ProcessFinikWebhookAsync(payload);
            return Ok(new { status = "success" });
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Error processing Finik webhook");
            return BadRequest(new { error = ex.Message });
        }
    }
}
