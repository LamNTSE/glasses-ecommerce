namespace OpticalStore.API.Requests.ProductVariants;

public sealed class ProductVariantRequest
{
    public string ProductId { get; set; } = string.Empty;

    public string? ColorName { get; set; }

    public string? FrameFinish { get; set; }

    public int? LensWidthMm { get; set; }

    public int? BridgeWidthMm { get; set; }

    public int? TempleLengthMm { get; set; }

    public string? SizeLabel { get; set; }

    public decimal? Price { get; set; }

    public int? Quantity { get; set; }

    public string Status { get; set; } = "ACTIVE";

    public string OrderItemType { get; set; } = "IN_STOCK";
}
