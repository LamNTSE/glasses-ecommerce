using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using OpticalStore.BLL.Exceptions;

namespace OpticalStore.API;

/// <summary>
/// Lưu ảnh đại diện vào wwwroot/uploads/avatars, trả về public path: /uploads/avatars/{name}
/// </summary>
internal static class UserAvatarStorage
{
    public const long MaxFileBytes = 5L * 1024 * 1024;

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public static async Task<string> SaveAsync(
        IFormFile file,
        IWebHostEnvironment env,
        CancellationToken cancellationToken)
    {
        if (file is not { Length: > 0 })
        {
            throw new AppException(
                "INVALID_AVATAR_FILE",
                "Tệp ảnh đại diện không hợp lệ.",
                HttpStatusCode.BadRequest);
        }

        if (file.Length > MaxFileBytes)
        {
            throw new AppException(
                "AVATAR_FILE_TOO_LARGE",
                "Ảnh đại diện không được vượt quá 5MB.",
                HttpStatusCode.BadRequest);
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new AppException(
                "AVATAR_FILE_TYPE",
                "Chỉ chấp nhận ảnh: .jpg, .jpeg, .png, .webp.",
                HttpStatusCode.BadRequest);
        }

        ext = ext.ToLowerInvariant();

        var webRoot = env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
        {
            webRoot = Path.Combine(env.ContentRootPath, "wwwroot");
        }

        var dir = Path.Combine(webRoot, "uploads", "avatars");
        Directory.CreateDirectory(dir);

        var name = $"{Guid.NewGuid():D}{ext}";
        var fullPath = Path.Combine(dir, name);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"/uploads/avatars/{name}";
    }
}
