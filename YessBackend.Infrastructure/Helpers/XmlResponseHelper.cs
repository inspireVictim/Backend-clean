using System.IO;
using System.Text;
using System.Xml.Linq;
using YessBackend.Application.DTOs.OptimaPayment;

namespace YessBackend.Infrastructure.Helpers;

/// <summary>
/// Вспомогательный класс для генерации XML ответов для платежной системы Optima
/// </summary>
public static class XmlResponseHelper
{
    /// <summary>
    /// Генерирует XML ответ в формате UTF-8
    /// </summary>
    public static string GenerateXmlResponse(OptimaPaymentResponseDto response)
    {
        var root = new XElement("response",
            new XElement("osmp_txn_id", response.OsmpTxnId ?? string.Empty)
        );

        // Добавляем prv_txn только если он задан
        if (!string.IsNullOrWhiteSpace(response.PrvTxn))
        {
            root.Add(new XElement("prv_txn", response.PrvTxn));
        }

        root.Add(new XElement("sum", response.Sum.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)));
        root.Add(new XElement("result", (int)response.Result));

        // Добавляем comment только если он задан
        if (!string.IsNullOrWhiteSpace(response.Comment))
        {
            root.Add(new XElement("comment", response.Comment));
        }

        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            root
        );

        var stringBuilder = new StringBuilder();
        using (var writer = new StringWriter(stringBuilder))
        {
            xml.Save(writer);
        }
        
        return stringBuilder.ToString();
    }
}

