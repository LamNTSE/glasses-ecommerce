using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Transaction
{
    public string Id { get; set; } = null!;

    public decimal? Amount { get; set; }

    public DateTime? DateTime { get; set; }

    public string? GatewayReference { get; set; }

    public string? Type { get; set; }

    public string? PaymentId { get; set; }

    public string? Description { get; set; }

    public virtual Payment? Payment { get; set; }
}
