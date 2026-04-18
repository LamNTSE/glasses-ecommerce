namespace OpticalStore.API.Requests.Orders;

public sealed class AcceptShipOrdersRequest
{
    public List<string> OrderIds { get; set; } = new();
}
