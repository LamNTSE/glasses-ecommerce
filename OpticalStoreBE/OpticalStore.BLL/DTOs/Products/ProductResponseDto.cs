namespace OpticalStore.BLL.DTOs.Products;

public sealed class ProductResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Brand { get; set; }

    public string? Category { get; set; }

    public string? FrameType { get; set; }

    public string? Gender { get; set; }

    public string? Shape { get; set; }

    public string? FrameMaterial { get; set; }

    public string? HingeType { get; set; }

    public string? NosePadType { get; set; }

    public decimal? WeightGram { get; set; }

    public decimal? MinPrice { get; set; }

    public decimal? MaxPrice { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? OrderItemType { get; set; }

    public string? ModelUrl { get; set; }

    public List<ProductImageDto> ImageUrl { get; set; } = new();
}
