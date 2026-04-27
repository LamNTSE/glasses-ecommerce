namespace OpticalStore.BLL.DTOs.Orders;

public sealed class PriceCheckItemDto
{
    public string ProductVariantId { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
