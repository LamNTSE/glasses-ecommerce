namespace OpticalStore.API.Requests.Combos;

public sealed class CartItemRequest
{
    public string SkuId { get; set; } = string.Empty;

    public int Quantity { get; set; }
}
