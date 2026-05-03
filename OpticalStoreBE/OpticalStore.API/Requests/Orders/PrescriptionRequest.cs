namespace OpticalStore.API.Requests.Orders;

public sealed class PrescriptionRequest
{
    public string? ImageUrl { get; set; }

    public double? OdSphere { get; set; }

    public double? OdCylinder { get; set; }

    /// <summary>Client có thể gửi số thập phân (parseFloat); map sang int khi convert DTO.</summary>
    public double? OdAxis { get; set; }

    public double? OdAdd { get; set; }

    public double? OdPd { get; set; }

    public double? OsSphere { get; set; }

    public double? OsCylinder { get; set; }

    public double? OsAxis { get; set; }

    public double? OsAdd { get; set; }

    public double? OsPd { get; set; }

    public string? Note { get; set; }
}
