using System;
using System.Collections.Generic;

namespace OpticalStore.DAL.Entities;

public partial class Order
{
    public string Id { get; set; } = null!;

    public DateTime? CreatedAt { get; set; }

    public string? DeliveryAddress { get; set; }

    public decimal? DepositAmount { get; set; }

    public string? PaymentMethod { get; set; }

    public string? PhoneNumber { get; set; }

    public string? Status { get; set; }

    public decimal? TotalAmount { get; set; }

    public string? CustomerId { get; set; }

    public string? PreOrderStatus { get; set; }

    public decimal? RemainingAmount { get; set; }

    public decimal? ComboDiscountAmount { get; set; }

    public string? ComboSnapshot { get; set; }

    public string? ComboId { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? ShippedAt { get; set; }

    public string? ShipperId { get; set; }

    public string? AccountHolderName { get; set; }

    public string? BankAccountNumber { get; set; }

    public string? BankName { get; set; }

    public string? RecipientName { get; set; }

    public virtual Combo? Combo { get; set; }

    public virtual User? Customer { get; set; }

    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<RefundRequest> RefundRequests { get; set; } = new List<RefundRequest>();
}
