namespace OpticalStore.API.Requests.Orders;

public sealed class BankInfoRequest
{
    public string? BankName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? AccountHolderName { get; set; }
}
