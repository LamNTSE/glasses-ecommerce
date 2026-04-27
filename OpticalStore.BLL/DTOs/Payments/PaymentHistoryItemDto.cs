namespace OpticalStore.BLL.DTOs.Payments;

public sealed class PaymentHistoryItemDto
{
    public string Id { get; set; } = string.Empty;

    public string? PaymentMethod { get; set; }

    public string? PaymentPurpose { get; set; }

    public decimal? Amount { get; set; }

    public decimal? Percentage { get; set; }

    public string? Status { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string? Description { get; set; }

    public string? TransactionReference { get; set; }
}
