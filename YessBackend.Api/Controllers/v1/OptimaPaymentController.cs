using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
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
            // Сохраняем IP-адрес для логирования
            var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            
            // Проверка IP-адреса (требование QIWI OSMP)
            var ipCheckEnabled = _configuration.GetValue<bool>("OptimaPayment:IpCheckEnabled", true);
            if (ipCheckEnabled)
            {
                var allowedIpRanges = _configuration.GetSection("OptimaPayment:AllowedIpRanges")
                    .Get<string[]>() ?? Array.Empty<string>();

                if (allowedIpRanges.Length > 0)
                {
                    var remoteIpAddress = HttpContext.Connection.RemoteIpAddress;
                    var isAllowed = IpAddressHelper.IsIpInAnySubnet(remoteIpAddress, allowedIpRanges);

                    if (!isAllowed)
                    {
                        _logger.LogWarning(
                            "Access denied for IP {RemoteIp} to Optima payment endpoint. Allowed ranges: {AllowedRanges}",
                            remoteIp,
                            string.Join(", ", allowedIpRanges));

                        // Возвращаем XML в формате QIWI при ошибке доступа
                        var errorResponse = new OptimaPaymentResponseDto
                        {
                            OsmpTxnId = txn_id ?? "unknown",
                            Sum = 0,
                            Result = OptimaResultCode.OtherError,
                            Comment = "Доступ запрещен"
                        };
                        Response.StatusCode = (int)HttpStatusCode.Forbidden;
                        return Content(
                            XmlResponseHelper.GenerateXmlResponse(errorResponse),
                            "application/xml; charset=utf-8");
                    }
                }
            }

            // Валидация обязательных параметров
            if (string.IsNullOrWhiteSpace(command))
            {
                var errorResponse = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txn_id ?? "unknown",
                    Sum = 0,
                    Result = OptimaResultCode.OtherError,
                    Comment = "Параметр command обязателен"
                };
                return Content(XmlResponseHelper.GenerateXmlResponse(errorResponse), "application/xml; charset=utf-8");
            }

            if (string.IsNullOrWhiteSpace(txn_id))
            {
                var errorResponse = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = "unknown",
                    Sum = 0,
                    Result = OptimaResultCode.OtherError,
                    Comment = "Параметр txn_id обязателен"
                };
                return Content(XmlResponseHelper.GenerateXmlResponse(errorResponse), "application/xml; charset=utf-8");
            }

            // Валидация txn_id: до 20 цифр (требование QIWI OSMP)
            if (txn_id.Length > 20 || !Regex.IsMatch(txn_id, @"^\d+$"))
            {
                _logger.LogWarning("Invalid txn_id format: {TxnId}", txn_id);
                var errorResponse = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txn_id,
                    Sum = 0,
                    Result = OptimaResultCode.OtherError,
                    Comment = "Неверный формат txn_id (до 20 цифр)"
                };
                return Content(XmlResponseHelper.GenerateXmlResponse(errorResponse), "application/xml; charset=utf-8");
            }

            if (string.IsNullOrWhiteSpace(account))
            {
                var errorResponse = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txn_id,
                    Sum = 0,
                    Result = OptimaResultCode.InvalidAccountFormat,
                    Comment = "Параметр account обязателен"
                };
                return Content(XmlResponseHelper.GenerateXmlResponse(errorResponse), "application/xml; charset=utf-8");
            }

            if (string.IsNullOrWhiteSpace(sum))
            {
                var errorResponse = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txn_id,
                    Sum = 0,
                    Result = OptimaResultCode.OtherError,
                    Comment = "Параметр sum обязателен"
                };
                return Content(XmlResponseHelper.GenerateXmlResponse(errorResponse), "application/xml; charset=utf-8");
            }

            // Валидация account по регулярному выражению (требование QIWI OSMP: до 50 символов, буквы, цифры, спецсимволы)
            // В нашем случае: только цифры от 1 до 10 символов
            var accountRegex = new Regex(@"^[0-9]{1,10}$", RegexOptions.Compiled);
            if (!accountRegex.IsMatch(account))
            {
                _logger.LogWarning("Invalid account format (regex validation): {Account}", account);
                var response = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txn_id,
                    Sum = 0,
                    Result = OptimaResultCode.InvalidAccountFormat,
                    Comment = "Неверный формат идентификатора абонента"
                };
                return Content(XmlResponseHelper.GenerateXmlResponse(response), "application/xml; charset=utf-8");
            }

            // Парсинг account (ID пользователя)
            if (!int.TryParse(account, out int accountId) || accountId <= 0)
            {
                _logger.LogWarning("Invalid account format (not a valid integer): {Account}", account);
                var response = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txn_id,
                    Sum = 0,
                    Result = OptimaResultCode.InvalidAccountFormat,
                    Comment = "Неверный формат идентификатора абонента"
                };
                return Content(XmlResponseHelper.GenerateXmlResponse(response), "application/xml; charset=utf-8");
            }

            // Валидация формата суммы (требование QIWI OSMP: дробное число с точностью до сотых, разделитель точка)
            // Формат: 10.45, 152.00 (всегда с точкой и двумя десятичными знаками)
            var sumRegex = new Regex(@"^\d+\.\d{2}$", RegexOptions.Compiled);
            if (!sumRegex.IsMatch(sum))
            {
                _logger.LogWarning("Invalid sum format (must be decimal with 2 decimal places): {Sum}", sum);
                var response = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txn_id,
                    Sum = 0,
                    Result = OptimaResultCode.OtherError,
                    Comment = "Неверный формат суммы (требуется формат: 10.45)"
                };
                return Content(XmlResponseHelper.GenerateXmlResponse(response), "application/xml; charset=utf-8");
            }

            // Парсинг суммы
            if (!decimal.TryParse(sum, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal sumDecimal) || sumDecimal <= 0)
            {
                _logger.LogWarning("Invalid sum format (not a valid decimal): {Sum}", sum);
                var response = new OptimaPaymentResponseDto
                {
                    OsmpTxnId = txn_id,
                    Sum = 0,
                    Result = OptimaResultCode.OtherError,
                    Comment = "Неверный формат суммы"
                };
                return Content(XmlResponseHelper.GenerateXmlResponse(response), "application/xml; charset=utf-8");
            }

            // Подготовка raw запроса для логирования (требование QIWI OSMP - сохранение для сверки)
            var rawRequest = $"?command={command}&txn_id={txn_id}&account={account}&sum={sum}" +
                           (!string.IsNullOrWhiteSpace(txn_date) ? $"&txn_date={txn_date}" : "");

            OptimaPaymentResponseDto responseDto;

            // Обработка команды check
            if (command.ToLowerInvariant() == "check")
            {
                responseDto = await _optimaPaymentService.CheckAccountAsync(
                    accountId, 
                    txn_id, 
                    sumDecimal,
                    ipAddress: remoteIp,
                    userAgent: Request.Headers["User-Agent"].ToString(),
                    rawRequest: rawRequest);
            }
            // Обработка команды pay
            else if (command.ToLowerInvariant() == "pay")
            {
                DateTime txnDate = DateTime.UtcNow;
                
                // Парсинг txn_date если передан
                if (!string.IsNullOrWhiteSpace(txn_date))
                {
                    // Формат: ГГГГММДДЧЧММСС (требование QIWI OSMP)
                    if (txn_date.Length == 14 &&
                        DateTime.TryParseExact(txn_date, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, 
                            DateTimeStyles.None, out DateTime parsedDate))
                    {
                        txnDate = parsedDate;
                    }
                    else
                    {
                        _logger.LogWarning("Invalid txn_date format: {TxnDate}. Expected format: yyyyMMddHHmmss", txn_date);
                    }
                }

                responseDto = await _optimaPaymentService.ProcessPaymentAsync(
                    accountId, 
                    txn_id, 
                    sumDecimal, 
                    txnDate,
                    ipAddress: remoteIp,
                    userAgent: Request.Headers["User-Agent"].ToString(),
                    rawRequest: rawRequest);
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

            // Генерируем XML ответ
            var xmlResponse = XmlResponseHelper.GenerateXmlResponse(responseDto);
            
            // Логирование успешной обработки
            _logger.LogInformation(
                "Optima payment processed: Command={Command}, TxnId={TxnId}, Account={Account}, Sum={Sum}, Result={Result}, IP={RemoteIp}",
                command, txn_id, account, sumDecimal, responseDto.Result, remoteIp);

            // Устанавливаем правильный Content-Type для XML UTF-8 (требование QIWI OSMP)
            Response.ContentType = "application/xml; charset=utf-8";
            return Content(xmlResponse, "application/xml; charset=utf-8");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Optima payment request: Command={Command}, TxnId={TxnId}, Account={Account}",
                command, txn_id, account);
            
            // При любой ошибке возвращаем XML в формате QIWI (требование протокола)
            var errorResponse = new OptimaPaymentResponseDto
            {
                OsmpTxnId = txn_id ?? "unknown",
                Sum = 0,
                Result = OptimaResultCode.OtherError,
                Comment = "Внутренняя ошибка сервера"
            };
            
            Response.ContentType = "application/xml; charset=utf-8";
            return Content(XmlResponseHelper.GenerateXmlResponse(errorResponse), "application/xml; charset=utf-8");
        }
    }
}

