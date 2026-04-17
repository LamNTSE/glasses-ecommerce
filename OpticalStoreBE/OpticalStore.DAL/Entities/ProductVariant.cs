using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class ProductVariant
{
    public string Id { get; set; } = null!;

    public int? BridgeWidthMm { get; set; }

    public string? ColorName { get; set; }

    public string? FrameFinish { get; set; }

    public int? LensWidthMm { get; set; }

    public decimal? Price { get; set; }

    public string? SizeLabel { get; set; }

    public string Status { get; set; } = null!;

    public int? TempleLengthMm { get; set; }

    public string ProductId { get; set; } = null!;

    public int? Quantity { get; set; }

    public bool? IsDeleted { get; set; }

    public string OrderItemType { get; set; } = null!;

    public virtual ICollection<ComboItem1c> ComboItem1cs { get; set; } = new List<ComboItem1c>();

    public virtual ICollection<ComboItem1> ComboItem1s { get; set; } = new List<ComboItem1>();

    public virtual ICollection<ComboItem> ComboItems { get; set; } = new List<ComboItem>();

    public virtual Inventory? Inventory { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Product Product { get; set; } = null!;
}
