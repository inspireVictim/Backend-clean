using Microsoft.AspNetCore.Http;

namespace YessBackend.Application.DTOs.Uploads;

public class UploadAvatarRequest
{
    public IFormFile Avatar { get; set; } = default!;
}
