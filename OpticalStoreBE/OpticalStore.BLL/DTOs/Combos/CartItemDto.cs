namespace OpticalStore.BLL.DTOs.Combos;

public sealed class CartItemDto
{
    public string SkuId { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
