namespace OpticalStore.BLL.DTOs.Refunds;

public sealed class RefundBatchDto
{
    public List<string> OrderIds { get; set; } = new();
}
