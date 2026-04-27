namespace OpticalStore.API.Requests.Orders;

public sealed class CreateOrderItemRequest
{
    public string ProductVariantId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? LensId { get; set; }

    public PrescriptionRequest? Prescription { get; set; }
}
