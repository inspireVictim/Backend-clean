using Microsoft.AspNetCore.Http;

namespace YessBackend.Application.DTOs.Uploads;

public class UploadPartnerCoverRequest
{
    public IFormFile Cover { get; set; } = default!;
}
