using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Len
{
    public string Id { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsDeleted { get; set; }

    public string? Material { get; set; }

    public string Name { get; set; } = null!;

    public decimal Price { get; set; }

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
