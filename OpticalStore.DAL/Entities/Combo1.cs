using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Combo1
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

    public virtual ICollection<ComboItem1c> ComboItem1cs { get; set; } = new List<ComboItem1c>();

    public virtual ICollection<ComboItem1> ComboItem1s { get; set; } = new List<ComboItem1>();
}
