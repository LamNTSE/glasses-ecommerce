namespace OpticalStore.BLL.DTOs.Lenses;

public sealed class LensResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Material { get; set; }

    public decimal Price { get; set; }

    public string? Description { get; set; }
}
