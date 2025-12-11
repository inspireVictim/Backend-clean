namespace YessBackend.Application.Interfaces.Payments;

public interface IFinikSignatureService
{
    string BuildCanonicalString(
        string httpMethod,
        string path,
        Dictionary<string, string> headers,
        Dictionary<string, string>? queryParameters = null,
        object? body = null);

    string GenerateSignature(string canonicalString, string privateKeyPem);

    bool VerifySignature(
        string canonicalString,
        string signatureBase64,
        string publicKeyPem);

    string SortJson(string json);
}
