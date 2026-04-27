namespace OpticalStore.BLL.DTOs.Orders;

public sealed class UpdateOrderDto
{
    public string? DeliveryAddress { get; set; }

    public string? RecipientName { get; set; }

    public string? PhoneNumber { get; set; }

    public List<UpdateOrderItemDto> Items { get; set; } = new();
}
