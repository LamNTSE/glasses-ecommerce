namespace OpticalStore.BLL.DTOs.Orders;

public sealed class CreateOrderDto
{
    public string DeliveryAddress { get; set; } = string.Empty;

    public string RecipientName { get; set; } = string.Empty;

    public string PhoneNumber { get; set; } = string.Empty;

    public List<CreateOrderItemDto> Items { get; set; } = new();

    public string? ComboId { get; set; }

    public BankInfoDto? BankInfo { get; set; }
}
