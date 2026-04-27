namespace OpticalStore.BLL.DTOs.Products;

public sealed class ProductUpsertDto
{
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

    public string Status { get; set; } = "ACTIVE";
}
