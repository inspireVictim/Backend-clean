using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YessBackend.Application.Config;
using YessBackend.Application.DTOs.FinikPayment;
using YessBackend.Application.Interfaces.Payments;

namespace YessBackend.Infrastructure.Services;

public class FinikPaymentService : IFinikPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly FinikPaymentConfig _config;
    private readonly IFinikSignatureService _signatureService;
    private readonly ILogger<FinikPaymentService> _logger;

    public FinikPaymentService(
        HttpClient httpClient,
        IOptions<FinikPaymentConfig> config,
        IFinikSignatureService signatureService,
        ILogger<FinikPaymentService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _signatureService = signatureService;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_config.ApiBaseUrl);
    }

    public async Task<FinikCreatePaymentResponseDto> CreatePaymentAsync(FinikCreatePaymentRequestDto request)
    {
        string paymentId = Guid.NewGuid().ToString();

        long startTs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long endTs = startTs + 86400000;

        var body = new
        {
            amount = request.Amount,
            cardType = "FINIK_QR",
            paymentId = paymentId,
            redirectUrl = request.RedirectUrl ?? _config.RedirectUrl,
            data = new
            {
                accountId = _config.AccountId,
                merchantCategoryCode = _config.MerchantCategoryCode,
                name_en = _config.QrName,
                webhookUrl = _config.WebhookUrl,
                description = request.Description,
                startDate = startTs,
                endDate = endTs
            }
        };

        string rawJson = JsonSerializer.Serialize(body);
        string sortedJson = _signatureService.SortJson(rawJson);

        _logger.LogWarning("JSON BODY (sorted):\n{J}", sortedJson);

        string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        string host = new Uri(_config.ApiBaseUrl).Host;

        var headers = new Dictionary<string, string>
        {
            { "host", host },
            { "x-api-key", _config.ApiKey },
            { "x-api-timestamp", timestamp }
        };

        string canonical = _signatureService.BuildCanonicalString(
            "POST",
            "/v1/payment",
            headers,
            null,
            JsonSerializer.Deserialize<object>(sortedJson)
        );

        _logger.LogWarning("CANONICAL:\n{C}", canonical);

        string privateKey = Environment.GetEnvironmentVariable("FINIK_PRIVATE_KEY")
            ?? throw new Exception("FINIK_PRIVATE_KEY missing");

        string signature = _signatureService.GenerateSignature(canonical, privateKey);

        _logger.LogWarning("SIGNATURE:\n{S}", signature);

        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/payment")
        {
            Content = new StringContent(sortedJson, Encoding.UTF8, "application/json")
        };

        req.Headers.Add("x-api-key", _config.ApiKey);
        req.Headers.Add("x-api-timestamp", timestamp);
        req.Headers.Add("signature", signature);

        var resp = await _httpClient.SendAsync(req);

        if ((int)resp.StatusCode == 302)
        {
            return new FinikCreatePaymentResponseDto
            {
                PaymentId = paymentId,
                PaymentUrl = resp.Headers.Location!.ToString()
            };
        }

        string err = await resp.Content.ReadAsStringAsync();
        _logger.LogError("Finik ERROR: {S} {B}", resp.StatusCode, err);

        throw new Exception($"Finik error: {resp.StatusCode} {err}");
    }

    public Task<bool> ProcessWebhookAsync(FinikWebhookDto webhook)
    {
        _logger.LogInformation("Finik webhook received: {Status}", webhook.Status);
        return Task.FromResult(true);
    }

    // ❗ Полностью удалили GetPaymentStatusAsync — он отсутствует в интерфейсе (вариант А)
}
