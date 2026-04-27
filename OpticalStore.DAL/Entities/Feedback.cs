using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Feedback
{
    public string Id { get; set; } = null!;

    public string? Comment { get; set; }

    public DateTime? CreatedAt { get; set; }

    public int Rating { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string CustomerId { get; set; } = null!;

    public string OrderId { get; set; } = null!;

    public string ProductId { get; set; } = null!;

    public virtual User Customer { get; set; } = null!;

    public virtual Order Order { get; set; } = null!;

    public virtual Product Product { get; set; } = null!;
}
