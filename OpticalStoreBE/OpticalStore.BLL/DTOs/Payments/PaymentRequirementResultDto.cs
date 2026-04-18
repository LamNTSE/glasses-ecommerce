namespace OpticalStore.BLL.DTOs.Payments;

public sealed class PaymentRequirementResultDto
{
    public decimal DepositPercentage { get; set; }

    public decimal RequiredAmount { get; set; }

    public decimal OrderTotal { get; set; }

    public decimal RequiredPaymentTotal { get; set; }

    public decimal RemainingPaymentTotal { get; set; }

    public List<PaymentRequirementItemResultDto> ItemRequirements { get; set; } = new();

    public bool AllowCod { get; set; }

    public string Message { get; set; } = string.Empty;
}
