namespace OpticalStore.API.Requests.ProductVariants;

public sealed class InventoryUpdateRequest
{
    public string ProductVariantId { get; set; } = string.Empty;

    public int ChangeAmount { get; set; }
}
