namespace OpticalStore.BLL;

/// <summary>
/// Chuẩn hóa URL ảnh sản phẩm trả về client (trình duyệt không đọc được scheme <c>local://</c>).
/// </summary>
public static class ProductImageUrl
{
    public static string? ResolveForClient(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var t = raw.Trim();
        if (t.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            t.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return t;
        }

        if (t.StartsWith("local://product/", StringComparison.OrdinalIgnoreCase))
        {
            var name = t["local://product/".Length..].TrimStart('/');
            return string.IsNullOrEmpty(name) ? null : $"/uploads/products/{name}";
        }

        return t;
    }
}
