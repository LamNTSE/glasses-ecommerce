namespace OpticalStore.BLL.DTOs.Orders;

public sealed class AcceptShipOrdersDto
{
    public List<string> OrderIds { get; set; } = new();
}
