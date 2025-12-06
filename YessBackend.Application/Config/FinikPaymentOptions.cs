namespace YessBackend.Application.Config;

public class FinikPaymentOptions
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = string.Empty;
    public string CallbackUrl { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public bool VerifySignature { get; set; } = true;
}
