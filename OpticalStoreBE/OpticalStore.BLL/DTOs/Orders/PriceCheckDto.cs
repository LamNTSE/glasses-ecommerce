namespace OpticalStore.BLL.DTOs.Orders;

public sealed class PriceCheckDto
{
    public List<PriceCheckItemDto> Items { get; set; } = new();

    public string? ComboId { get; set; }
}
