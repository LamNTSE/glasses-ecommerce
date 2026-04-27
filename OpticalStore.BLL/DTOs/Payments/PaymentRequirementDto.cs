namespace OpticalStore.BLL.DTOs.Payments;

public sealed class PaymentRequirementDto
{
    public List<PaymentRequirementItemDto> Items { get; set; } = new();
}
