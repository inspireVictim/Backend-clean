using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Net;
using System.Text;
using YessBackend.Application.DTOs.OptimaPayment;
using YessBackend.Application.Enums;
using YessBackend.Application.Services;
using YessBackend.Infrastructure.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace YessBackend.Api.Controllers.v1;

/// <summary>
/// Контроллер для приема платежей от Optima Bank
/// Реализует протокол QIWI (как пример)
/// </summary>
[ApiController]
[Route("api/v1/optima/payment")]
[Tags("Optima Payment")]
public class OptimaPaymentController : ControllerBase
{
    private readonly IOptimaPaymentService _optimaPaymentService;
    private readonly ILogger<OptimaPaymentController> _logger;
    private readonly IConfiguration _configuration;

    public OptimaPaymentController(
        IOptimaPaymentService optimaPaymentService,
        ILogger<OptimaPaymentController> logger,
        IConfiguration configuration)
    {
        _optimaPaymentService = optimaPaymentService;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Обработка запросов от Optima Bank
    /// </summary>
    /// <remarks>
    /// Примеры запросов:
    /// 
    /// Проверка счета:
    /// GET /api/v1/optima/payment?command=check&txn_id=1234567&account=1&sum=10.45
    /// 
    /// Пополнение баланса:
    /// GET /api/v1/optima/payment?command=pay&txn_id=1234567&txn_date=20090815120133&account=1&sum=10.45
    /// </remarks>
    /// <param name="command">Тип команды: check (проверка) или pay (пополнение)</param>
    /// <param name="txn_id">Уникальный идентификатор транзакции в системе Optima</param>
    /// <param name="account">ID пользователя в нашей системе</param>
    /// <param name="sum">Сумма платежа (формат: 10.45)</param>
    /// <param name="txn_date">Дата платежа в формате ГГГГММДДЧЧММСС (только для pay)</param>
    /// <returns>XML ответ с результатом операции</returns>
    [HttpGet]
    [Produces("application/xml", "text/xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK, "application/xml")]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest, "application/xml")]
    public async Task<IActionResult> ProcessPayment(
        [FromQuery] string command,
        [FromQuery] string txn_id,
        [FromQuery] string account,
        [FromQuery] string sum,
        [FromQuery] string? txn_date = null)
    {
        try
        {
            // Проверка IP-адреса
            var ipCheckEnabled = _configuration.GetValue<bool>("OptimaPayment:IpCheckEnabled", true);
            if (ipCheckEnabled)
            {
                var allowedIpRanges = _configuration.GetSection("OptimaPayment:AllowedIpRanges")
                    .Get<string[]>() ?? Array.Empty<string>();

                if (allowedIpRanges.Length > 0)
                {
                    var remoteIp = HttpContext.Connection.RemoteIpAddress;
                    var isAllowed = IpAddressHelper.IsIpInAnySubnet(remoteIp, allowedIpRanges);

                    if (!isAllowed)
                    {
                        _logger.LogWarning(
                            "Access denied for IP {RemoteIp} to Optima payment endpoint. Allowed ranges: {AllowedRanges}",
                            remoteIp,
                            string.Join(", ", allowedIpRanges));

                        Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        return Content(
                            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><error><message>Access denied</message></error>",
                            "application/xml; charset=utf-8");
                    }
                }
            }

            // Валидация обязательных параметров
            if (string.IsNullOrWhiteSpace(command))
            {
                return BadRequest(GenerateErrorXml("command", "Параметр command обязателен"));
            }

            if (string.IsNullOrWhiteSpace(txn_id))
            {
                return BadRequest(GenerateErrorXml("txn_id", "Параметр txn_id обязателен"));
            }

            if (string.IsNullOrWhiteSpace(account))
            {
                return BadRequest(GenerateErrorXml("account", "Параметр account обязателен"));
            }

            if (string.IsNullOrWhiteSpace(sum))
            {
                return BadRequest(GenerateErrorXml("sum", "Параметр sum обязателен"));
            }

            // Парсинг account (ID пользователя)
            if (!int.TryParse(account, out int accountId))
            {
                _logger.LogWarning("Invalid account format: {Account}", account);
                var response = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txn_id,
                    Sum = 0,
                    Result = OptimaResultCode.InvalidAccountFormat,
                    Comment = "Неверный формат идентификатора абонента"
                };
                return Content(XmlResponseHelper.GenerateXmlResponse(response), "application/xml; charset=utf-8");
            }

            // Парсинг суммы
            if (!decimal.TryParse(sum, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal sumDecimal))
            {
                _logger.LogWarning("Invalid sum format: {Sum}", sum);
                var response = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txn_id,
                    Sum = 0,
                    Result = OptimaResultCode.OtherError,
                    Comment = "Неверный формат суммы"
                };
                return Content(XmlResponseHelper.GenerateXmlResponse(response), "application/xml; charset=utf-8");
            }

            OptimaPaymentResponseDto responseDto;

            // Обработка команды check
            if (command.ToLowerInvariant() == "check")
            {
                responseDto = await _optimaPaymentService.CheckAccountAsync(accountId, txn_id, sumDecimal);
            }
            // Обработка команды pay
            else if (command.ToLowerInvariant() == "pay")
            {
                DateTime txnDate = DateTime.UtcNow;
                
                // Парсинг txn_date если передан
                if (!string.IsNullOrWhiteSpace(txn_date))
                {
                    // Формат: ГГГГММДДЧЧММСС
                    if (txn_date.Length == 14 &&
                        DateTime.TryParseExact(txn_date, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, 
                            DateTimeStyles.None, out DateTime parsedDate))
                    {
                        txnDate = parsedDate;
                    }
                }

                responseDto = await _optimaPaymentService.ProcessPaymentAsync(accountId, txn_id, sumDecimal, txnDate);
            }
            else
            {
                _logger.LogWarning("Unknown command: {Command}", command);
                responseDto = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txn_id,
                    Sum = sumDecimal,
                    Result = OptimaResultCode.OtherError,
                    Comment = $"Неизвестная команда: {command}"
                };
            }

            var xmlResponse = XmlResponseHelper.GenerateXmlResponse(responseDto);
            return Content(xmlResponse, "application/xml; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Optima payment request");
            
            var errorResponse = new OptimaPaymentResponseDto
            {
                OsmpTxnId = txn_id ?? "unknown",
                Sum = 0,
                Result = OptimaResultCode.OtherError,
                Comment = "Внутренняя ошибка сервера"
            };
            
            return Content(XmlResponseHelper.GenerateXmlResponse(errorResponse), "application/xml; charset=utf-8");
        }
    }

    private string GenerateErrorXml(string parameter, string message)
    {
        return $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><error><parameter>{parameter}</parameter><message>{message}</message></error>";
    }
}

