namespace OpticalStore.API.Requests.Payments;

public sealed class PaymentRequirementRequest
{
    public List<PaymentRequirementItemRequest> Items { get; set; } = new();
}
