using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Prescription
{
    public string Id { get; set; } = null!;

    public string? Note { get; set; }

    public double? OdAdd { get; set; }

    public int? OdAxis { get; set; }

    public double? OdCylinder { get; set; }

    public double? OdPd { get; set; }

    public double? OdSphere { get; set; }

    public double? OsAdd { get; set; }

    public int? OsAxis { get; set; }

    public double? OsCylinder { get; set; }

    public double? OsPd { get; set; }

    public double? OsSphere { get; set; }

    public string? ImageUrl { get; set; }

    public virtual OrderItem? OrderItem { get; set; }
}
