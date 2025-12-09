using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.FileProviders;
using YessBackend.Application.Services;

namespace YessBackend.Infrastructure.Services;

public class StorageService : IStorageService
{
    private readonly ILogger<StorageService> _logger;
    private readonly string _uploadsRoot;
    private readonly string _publicBaseUrl;

    public StorageService(IConfiguration configuration, ILogger<StorageService> logger)
    {
        _logger = logger;

        // Путь к папке uploads
        _uploadsRoot = Path.Combine(Directory.GetCurrentDirectory(), "uploads");

        if (!Directory.Exists(_uploadsRoot))
            Directory.CreateDirectory(_uploadsRoot);

        _publicBaseUrl = configuration["PublicBaseUrl"]
                         ?? "http://localhost:5000";
    }

    public async Task<string> SaveFileAsync(IFormFile file, string folder, CancellationToken cancellationToken = default)
    {
        // Разрешённые расширения
        var allowedExt = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExt.Contains(ext))
            throw new InvalidOperationException("Недопустимый формат файла");

        if (file.Length > 10 * 1024 * 1024)
            throw new InvalidOperationException("Размер файла превышает 10MB");

        // Создаём поддиректорию
        var targetFolder = Path.Combine(_uploadsRoot, folder);
        if (!Directory.Exists(targetFolder))
            Directory.CreateDirectory(targetFolder);

        // Генерируем уникальное имя
        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(targetFolder, fileName);

        // Сохраняем файл
        await using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        // Формируем URL для MAUI и фронта
        var url = $"{_publicBaseUrl}/uploads/{folder}/{fileName}";
        return url;
    }

    public Task<bool> DeleteFileAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            if (!fileUrl.Contains("/uploads/"))
                return Task.FromResult(false);

            var relative = fileUrl.Split("/uploads/")[1];
            var path = Path.Combine(_uploadsRoot, relative.Replace("/", Path.DirectorySeparatorChar.ToString()));

            if (File.Exists(path))
            {
                File.Delete(path);
                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    public Task<bool> FileExistsAsync(string fileUrl, CancellationToken cancellationToken = default)
    {
        if (!fileUrl.Contains("/uploads/"))
            return Task.FromResult(false);

        var relative = fileUrl.Split("/uploads/")[1];
        var path = Path.Combine(_uploadsRoot, relative.Replace("/", Path.DirectorySeparatorChar.ToString()));

        return Task.FromResult(File.Exists(path));
    }
}
