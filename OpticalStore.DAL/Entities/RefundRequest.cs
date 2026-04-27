using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class RefundRequest
{
    public string Id { get; set; } = null!;

    public string? AccountHolderName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BankName { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public string? CustomerId { get; set; }

    public string? ProcessedBy { get; set; }

    public decimal? RefundAmount { get; set; }

    public string? Status { get; set; }

    public string? OrderId { get; set; }

    public decimal? OrderTotalAmount { get; set; }

    public string? VariantId { get; set; }

    public decimal? RefundPercentage { get; set; }

    public decimal? DeductionAmount { get; set; }

    public string? PaymentId { get; set; }

    public virtual Order? Order { get; set; }

    public virtual Payment? Payment { get; set; }
}
