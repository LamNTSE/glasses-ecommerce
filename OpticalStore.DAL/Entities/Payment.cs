using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Payment
{
    public string Id { get; set; } = null!;

    public decimal? Amount { get; set; }

    public DateTime? PaymentDate { get; set; }

    public string? PaymentMethod { get; set; }

    public string? PaymentPurpose { get; set; }

    public string? Status { get; set; }

    public string? OrderId { get; set; }

    public string? Description { get; set; }

    public decimal? Percentage { get; set; }

    public virtual Order? Order { get; set; }

    public virtual ICollection<RefundRequest> RefundRequests { get; set; } = new List<RefundRequest>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
