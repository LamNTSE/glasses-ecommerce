using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class OrderStatusHistory
{
    public string Id { get; set; } = null!;

    public DateTime? ChangedAt { get; set; }

    public string? NewStatus { get; set; }

    public string? OldStatus { get; set; }

    public string? OrderId { get; set; }
}
