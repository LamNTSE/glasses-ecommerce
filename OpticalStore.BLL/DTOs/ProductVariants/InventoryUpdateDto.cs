namespace OpticalStore.BLL.DTOs.ProductVariants;

public sealed class InventoryUpdateDto
{
    public string ProductVariantId { get; set; } = string.Empty;

    public int ChangeAmount { get; set; }
}
