namespace OpticalStore.API.Requests.Orders;

public sealed class PriceCheckItemRequest
{
    public string ProductVariantId { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
