using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using YessBackend.Application.Config;
using YessBackend.Application.DTOs.FinikPayment;
using YessBackend.Application.Interfaces.Payments;
using YessBackend.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Security.Cryptography;

namespace YessBackend.Infrastructure.Services;

public class FinikPaymentService : IFinikPaymentService
{
    private readonly HttpClient _httpClient;
    private readonly FinikPaymentConfig _config;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<FinikPaymentService> _logger;

    public FinikPaymentService(
        HttpClient httpClient,
        IOptions<FinikPaymentConfig> config,
        ApplicationDbContext db,
        ILogger<FinikPaymentService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _db = db;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_config.ApiBaseUrl);
    }

    public async Task<FinikPaymentResponseDto> CreatePaymentAsync(FinikPaymentRequestDto request)
    {
        var body = new
        {
            account_id = _config.AccountId,
            amount = request.Amount,
            currency = "KGS",
            description = request.Description ?? $"Order #{request.OrderId}",
            external_id = request.OrderId.ToString(),
            success_url = request.SuccessUrl,
            cancel_url = request.CancelUrl,
            callback_url = _config.CallbackUrl
        };

        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

        var response = await _httpClient.PostAsJsonAsync("/api/v1/payments", body);

        var payload = await response.Content.ReadFromJsonAsync<FinikPaymentResponseDto>();

        if (payload == null)
            throw new Exception("Finik API вернул пустой ответ");

        return payload;
    }

    public async Task<FinikWebhookDto> GetPaymentStatusAsync(string paymentId)
    {
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_config.ClientId}:{_config.ClientSecret}"));
        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

        var response = await _httpClient.GetAsync($"/api/v1/payments/{paymentId}");

        var payload = await response.Content.ReadFromJsonAsync<FinikWebhookDto>();

        if (payload == null)
            throw new Exception("Finik API вернул пустой ответ");

        return payload;
    }

    public async Task<bool> ProcessWebhookAsync(FinikWebhookDto webhook)
    {
        // Пока просто логируем — можно добавить обновление заказа
        _logger.LogInformation("Finik webhook: {PaymentId} status={Status}",
            webhook.PaymentId, webhook.Status);

        return true;
    }
}
