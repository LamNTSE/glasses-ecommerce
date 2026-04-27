namespace OpticalStore.API.Requests.Orders;

public sealed class UpdateOrderItemRequest
{
    public string OrderItemId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public PrescriptionRequest? Prescription { get; set; }
}
