using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Combo
{
    public string Id { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public string? Description { get; set; }

    public string DiscountType { get; set; } = null!;

    public decimal DiscountValue { get; set; }

    public DateTime EndTime { get; set; }

    public bool IsManuallyDisabled { get; set; }

    public string Name { get; set; } = null!;

    public DateTime StartTime { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? UpdatedAt { get; set; }

    public bool? IsDeleted { get; set; }

    public virtual ICollection<ComboItem> ComboItems { get; set; } = new List<ComboItem>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
