namespace OpticalStore.BLL.DTOs.Orders;

public sealed class PrescriptionDto
{
    public string? ImageUrl { get; set; }

    public double? OdSphere { get; set; }

    public double? OdCylinder { get; set; }

    public int? OdAxis { get; set; }

    public double? OdAdd { get; set; }

    public double? OdPd { get; set; }

    public double? OsSphere { get; set; }

    public double? OsCylinder { get; set; }

    public int? OsAxis { get; set; }

    public double? OsAdd { get; set; }

    public double? OsPd { get; set; }

    public string? Note { get; set; }
}
