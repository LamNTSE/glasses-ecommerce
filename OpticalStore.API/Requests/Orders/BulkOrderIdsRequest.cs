namespace OpticalStore.API.Requests.Orders;

public sealed class BulkOrderIdsRequest
{
    public List<string> OrderIds { get; set; } = new();
}