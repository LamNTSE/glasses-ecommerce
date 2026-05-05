using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using OpticalStore.BLL.Exceptions;

namespace OpticalStore.API;

/// <summary>
/// Luu anh xac nhan giao hang vao wwwroot/uploads/delivery-proofs, tra ve public path.
/// </summary>
internal static class DeliveryProofImageStorage
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
                "INVALID_DELIVERY_PROOF_FILE",
                "Delivery proof image is required.",
                HttpStatusCode.BadRequest);
        }

        if (file.Length > MaxFileBytes)
        {
            throw new AppException(
                "DELIVERY_PROOF_FILE_TOO_LARGE",
                "Delivery proof image must not exceed 5MB.",
                HttpStatusCode.BadRequest);
        }

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedExtensions.Contains(ext))
        {
            throw new AppException(
                "DELIVERY_PROOF_FILE_TYPE",
                "Only image files are accepted: .jpg, .jpeg, .png, .webp.",
                HttpStatusCode.BadRequest);
        }

        ext = ext.ToLowerInvariant();

        var webRoot = env.WebRootPath;
        if (string.IsNullOrEmpty(webRoot))
        {
            webRoot = Path.Combine(env.ContentRootPath, "wwwroot");
        }

        var dir = Path.Combine(webRoot, "uploads", "delivery-proofs");
        Directory.CreateDirectory(dir);

        var name = $"{Guid.NewGuid():D}{ext}";
        var fullPath = Path.Combine(dir, name);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        return $"/uploads/delivery-proofs/{name}";
    }
}
