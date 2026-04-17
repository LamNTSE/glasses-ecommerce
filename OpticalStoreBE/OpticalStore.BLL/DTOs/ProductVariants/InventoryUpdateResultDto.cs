namespace OpticalStore.BLL.DTOs.ProductVariants;

public sealed class InventoryUpdateResultDto
{
    public ProductVariantDto ProductVariant { get; set; } = new();

    public int UpdatedOrderCount { get; set; }

    public List<object> UpdatedOrders { get; set; } = new();
}
