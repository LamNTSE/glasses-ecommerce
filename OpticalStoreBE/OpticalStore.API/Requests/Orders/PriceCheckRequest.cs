namespace OpticalStore.API.Requests.Orders;

public sealed class PriceCheckRequest
{
    public List<PriceCheckItemRequest> Items { get; set; } = new();

    public string? ComboId { get; set; }
}
