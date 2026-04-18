namespace OpticalStore.API.Requests.Refunds;

public sealed class RefundBatchRequest
{
    public List<string> OrderIds { get; set; } = new();
}
