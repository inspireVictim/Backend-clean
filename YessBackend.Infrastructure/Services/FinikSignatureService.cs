using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YessBackend.Application.Interfaces.Payments;

namespace YessBackend.Infrastructure.Services;

public class FinikSignatureService : IFinikSignatureService
{
    public string BuildCanonicalString(
        string httpMethod,
        string path,
        Dictionary<string, string> headers,
        Dictionary<string, string>? queryParameters = null,
        object? body = null)
    {
        var sb = new StringBuilder();


        // 1) Метод
        sb.Append(httpMethod.ToLowerInvariant()).Append('\n');

        // 2) Путь
        sb.Append(path).Append('\n');

        // 3) Заголовки — строго отсортированные и В ОДНУ СТРОКУ
        var filtered = headers
            .Where(h =>
                h.Key.Equals("host", StringComparison.OrdinalIgnoreCase) ||
                h.Key.StartsWith("x-api-", StringComparison.OrdinalIgnoreCase))
            .OrderBy(h => h.Key, StringComparer.Ordinal)
            .Select(h => $"{h.Key.ToLower()}:{h.Value}");

        sb.Append(string.Join("&", filtered)).Append('\n');

        // 4) Тело — компактный JSON
        string json = body != null
            ? JsonSerializer.Serialize(body, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            })
            : "";

        sb.Append(json);

        return sb.ToString();
    }

    // Требуется интерфейсом — оставляем как есть
    public string SortJson(string json) => json;

    public string GenerateSignature(string canonicalString, string privateKeyPem)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        byte[] data = Encoding.UTF8.GetBytes(canonicalString);
        byte[] signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return Convert.ToBase64String(signature);
    }

    public bool VerifySignature(string canonicalString, string signatureBase64, string publicKeyPem)
    {
        using RSA rsa = RSA.Create();
        rsa.ImportFromPem(publicKeyPem);

        byte[] data = Encoding.UTF8.GetBytes(canonicalString);
        byte[] signature = Convert.FromBase64String(signatureBase64);

        return rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
    }
}
