namespace OpticalStore.API.Requests.Orders;

public sealed class UpdateOrderRequest
{
    public string? DeliveryAddress { get; set; }

    public string? RecipientName { get; set; }

    public string? PhoneNumber { get; set; }

    public List<UpdateOrderItemRequest> Items { get; set; } = new();
}
