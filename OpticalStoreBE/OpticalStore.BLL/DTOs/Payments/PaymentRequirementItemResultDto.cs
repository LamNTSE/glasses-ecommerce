namespace OpticalStore.BLL.DTOs.Payments;

public sealed class PaymentRequirementItemResultDto
{
    public string? OrderItemId { get; set; }

    public string OrderItemType { get; set; } = "IN_STOCK";

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal LensPrice { get; set; }

    public decimal LensPriceTotal { get; set; }

    public decimal BaseItemTotal { get; set; }

    public decimal ItemTotal { get; set; }

    public decimal PaymentPercentage { get; set; }

    public decimal RequiredPayment { get; set; }
}
