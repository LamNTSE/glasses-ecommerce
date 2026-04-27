namespace OpticalStore.API.Requests.Payments;

public sealed class PaymentRequirementItemRequest
{
    public string? ProductVariantId { get; set; }

    public string? LensId { get; set; }

    public int Quantity { get; set; }
}
