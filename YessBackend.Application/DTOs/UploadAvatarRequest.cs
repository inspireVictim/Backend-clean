using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace YessBackend.Application.DTOs
{
    public class UploadAvatarRequest
    {
        [Required]
        public IFormFile Avatar { get; set; }
    }
}
