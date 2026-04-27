using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class OrderItem
{
    public string Id { get; set; } = null!;

    public string? OrderItemType { get; set; }

    public int? Quantity { get; set; }

    public string? Status { get; set; }

    public decimal? TotalPrice { get; set; }

    public decimal? UnitPrice { get; set; }

    public string? InventoryId { get; set; }

    public string? OrderId { get; set; }

    public string? PrescriptionId { get; set; }

    public decimal? DepositPrice { get; set; }

    public decimal? RemainingPrice { get; set; }

    public decimal? LensPrice { get; set; }

    public string? LensId { get; set; }

    public string? LensName { get; set; }

    public string? ProductVariantId { get; set; }

    public virtual Inventory? Inventory { get; set; }

    public virtual Len? Lens { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Prescription? Prescription { get; set; }

    public virtual ProductVariant? ProductVariant { get; set; }
}
