using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class ProductImage
{
    public string Id { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public string? ProductId { get; set; }

    public virtual Product? Product { get; set; }
}
