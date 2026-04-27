namespace OpticalStore.BLL.DTOs.Orders;

public sealed class UpdateOrderItemDto
{
    public string OrderItemId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public PrescriptionDto? Prescription { get; set; }
}
