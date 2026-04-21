namespace OpticalStore.BLL.Configuration;

public sealed class VnpayOptions
{
    public const string SectionName = "Vnpay";

    public string TmnCode { get; set; } = string.Empty;

    public string HashSecret { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string ReturnUrl { get; set; } = string.Empty;

    public string IpnUrl { get; set; } = string.Empty;

    public string FrontendBaseUrl { get; set; } = string.Empty;

    public string Version { get; set; } = "2.1.0";

    public string Locale { get; set; } = "vn";

    public string CurrencyCode { get; set; } = "VND";

    public string OrderType { get; set; } = "other";

    public int ExpireMinutes { get; set; } = 15;
}