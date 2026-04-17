using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Inventory
{
    public string Id { get; set; } = null!;

    public int? Quantity { get; set; }

    public int? ReservedQuantity { get; set; }

    public string? ProductVariantId { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ProductVariant? ProductVariant { get; set; }
}
