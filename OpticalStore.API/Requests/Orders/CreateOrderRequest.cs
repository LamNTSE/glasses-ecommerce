namespace OpticalStore.API.Requests.Orders;

public sealed class CreateOrderRequest
{
    public string DeliveryAddress { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public List<CreateOrderItemRequest> Items { get; set; } = new();

    public BankInfoRequest? BankInfo { get; set; }
}
