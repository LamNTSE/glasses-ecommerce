namespace OpticalStore.BLL.DTOs.Orders;

public sealed class CreateOrderItemDto
{
    public string ProductVariantId { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? LensId { get; set; }

    public PrescriptionDto? Prescription { get; set; }
}
