using Microsoft.AspNetCore.Http;

namespace YessBackend.Application.DTOs.Uploads;

public class UploadPartnerLogoRequest
{
    public IFormFile Logo { get; set; } = default!;
}
