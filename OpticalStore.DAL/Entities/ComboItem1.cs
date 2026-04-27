using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class ComboItem1
{
    public string Id { get; set; } = null!;

    public int RequiredQuantity { get; set; }

    public string ComboId { get; set; } = null!;

    public string? ProductId { get; set; }

    public string? ProductVariantId { get; set; }

    public virtual Combo1 Combo { get; set; } = null!;

    public virtual Product? Product { get; set; }

    public virtual ProductVariant? ProductVariant { get; set; }
}
