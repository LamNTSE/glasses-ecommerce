namespace OpticalStore.API.Requests.Lenses;

public sealed class CreateLensRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Material { get; set; }

    public decimal Price { get; set; }

    public string? Description { get; set; }
}
