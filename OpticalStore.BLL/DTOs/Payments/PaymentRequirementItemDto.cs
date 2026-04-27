namespace OpticalStore.BLL.DTOs.Payments;

public sealed class PaymentRequirementItemDto
{
    public string? ProductVariantId { get; set; }

    public string? LensId { get; set; }

    public int Quantity { get; set; }
}
