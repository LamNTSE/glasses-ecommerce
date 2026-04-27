using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using OpticalStore.BLL.Exceptions;

namespace OpticalStore.API;

/// <summary>
/// Lưu ảnh sản phẩm vào wwwroot/uploads/products, trả về public path: /uploads/products/{name}
/// </summary>
internal static class ProductImageStorage
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
                "INVALID_PRODUCT_IMAGE",
                "Tệp ảnh sản phẩm không hợp lệ.",
                HttpStatusCode.BadRequest);
        }

        if (file.Length > MaxFileBytes)
        {
            throw new AppException(
                "PRODUCT_IMAGE_TOO_LARGE",
                "Ảnh sản phẩm không được vượt quá 5MB.",
                HttpStatusCode.BadRequest);
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new AppException(
                "PRODUCT_IMAGE_TYPE",
                "Chỉ chấp nhận ảnh: .jpg, .jpeg, .png, .webp.",
                HttpStatusCode.BadRequest);
        }

        ext = ext.ToLowerInvariant();

        var webRoot = env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
        {
            webRoot = Path.Combine(env.ContentRootPath, "wwwroot");
        }

        var dir = Path.Combine(webRoot, "uploads", "products");
        Directory.CreateDirectory(dir);

        var name = $"{Guid.NewGuid():D}{ext}";
        var fullPath = Path.Combine(dir, name);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"/uploads/products/{name}";
    }
}
