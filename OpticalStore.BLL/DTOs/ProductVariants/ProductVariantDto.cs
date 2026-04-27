namespace OpticalStore.BLL.DTOs.ProductVariants;

public sealed class ProductVariantDto
{
    public string Id { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public string? ColorName { get; set; }

    public string? FrameFinish { get; set; }

    public int? LensWidthMm { get; set; }

    public int? BridgeWidthMm { get; set; }

    public int? TempleLengthMm { get; set; }

    public string? SizeLabel { get; set; }

    public decimal? Price { get; set; }

    public int? Quantity { get; set; }

    public string Status { get; set; } = string.Empty;

    public string OrderItemType { get; set; } = string.Empty;
}
