using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace OpticalStore.API;

internal static class PrescriptionImageStorage
{
    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

    /// <summary>
    /// Lưu file upload vào wwwroot và trả về đường dẫn tương đối (bắt đầu bằng /) để FE ghép với base URL API.
    /// </summary>
    public static async Task<string> SaveAsync(IFormFile file, IWebHostEnvironment env, CancellationToken cancellationToken)
    {
        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext))
        {
            ext = ".jpg";
        }

        ext = ext.ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            ext = ".jpg";
        }

        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var dir = Path.Combine(webRoot, "uploads", "prescriptions");
        Directory.CreateDirectory(dir);

        var name = $"{Guid.NewGuid():N}{ext}";
        var fullPath = Path.Combine(dir, name);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"/uploads/prescriptions/{name}";
    }
}
