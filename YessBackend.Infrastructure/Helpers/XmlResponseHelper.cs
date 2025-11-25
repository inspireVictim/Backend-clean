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
    /// Порядок элементов согласно QIWI OSMP v1.4: osmp_txn_id, prv_txn (если есть), sum, result, comment
    /// </summary>
    public static string GenerateXmlResponse(OptimaPaymentResponseDto response)
    {
        // Создаем элементы в правильном порядке согласно QIWI OSMP v1.4
        var elements = new List<XElement>
        {
            // 1. osmp_txn_id - всегда присутствует
            new XElement("osmp_txn_id", response.OsmpTxnId ?? string.Empty)
        };

        // 2. prv_txn - только для команды pay (если задан), после osmp_txn_id
        if (!string.IsNullOrWhiteSpace(response.PrvTxn))
        {
            elements.Add(new XElement("prv_txn", response.PrvTxn));
        }

        // 3. sum - всегда присутствует (формат: 10.45)
        elements.Add(new XElement("sum", response.Sum.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)));
        
        // 4. result - всегда присутствует
        elements.Add(new XElement("result", (int)response.Result));

        // 5. comment - всегда присутствует (может быть пустым)
        elements.Add(new XElement("comment", response.Comment ?? string.Empty));

        var root = new XElement("response", elements);

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

