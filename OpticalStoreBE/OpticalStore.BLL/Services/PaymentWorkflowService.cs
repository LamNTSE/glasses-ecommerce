using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Payments;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class PaymentWorkflowService : IPaymentWorkflowService
{
    private readonly OpticalStoreDbContext _dbContext;

    public PaymentWorkflowService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PaymentRequirementResultDto> GetPaymentRequirementAsync(PaymentRequirementDto request, CancellationToken cancellationToken = default)
    {
        var itemRequirements = new List<PaymentRequirementItemResultDto>();
        decimal orderTotal = 0m;
        decimal requiredPaymentTotal = 0m;

        foreach (var item in request.Items)
        {
            if (item.Quantity < 1)
            {
                throw new AppException("INVALID_QUANTITY", "Quantity must be >= 1.", HttpStatusCode.BadRequest);
            }

            var variant = item.ProductVariantId is null
                ? null
                : await _dbContext.ProductVariants.FirstOrDefaultAsync(x => x.Id == item.ProductVariantId && !(x.IsDeleted ?? false), cancellationToken);
            var lens = item.LensId is null
                ? null
                : await _dbContext.Lens.FirstOrDefaultAsync(x => x.Id == item.LensId && !x.IsDeleted, cancellationToken);

            var unitPrice = variant?.Price ?? 0m;
            var lensPrice = lens?.Price ?? 0m;
            var baseItemTotal = unitPrice * item.Quantity;
            var lensPriceTotal = lensPrice * item.Quantity;
            var itemTotal = baseItemTotal + lensPriceTotal;

            var paymentPercentage = string.Equals(variant?.OrderItemType, "PRE_ORDER", StringComparison.OrdinalIgnoreCase) ? 0.5m : 1m;
            var requiredPayment = baseItemTotal * paymentPercentage + lensPriceTotal;

            orderTotal += itemTotal;
            requiredPaymentTotal += requiredPayment;

            itemRequirements.Add(new PaymentRequirementItemResultDto
            {
                OrderItemId = null,
                OrderItemType = variant?.OrderItemType ?? "IN_STOCK",
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                LensPrice = lensPrice,
                LensPriceTotal = lensPriceTotal,
                BaseItemTotal = baseItemTotal,
                ItemTotal = itemTotal,
                PaymentPercentage = paymentPercentage,
                RequiredPayment = requiredPayment
            });
        }

        var remaining = Math.Max(0m, orderTotal - requiredPaymentTotal);

        return new PaymentRequirementResultDto
        {
            DepositPercentage = 0.5m,
            RequiredAmount = requiredPaymentTotal,
            OrderTotal = orderTotal,
            RequiredPaymentTotal = requiredPaymentTotal,
            RemainingPaymentTotal = remaining,
            ItemRequirements = itemRequirements,
            AllowCod = requiredPaymentTotal == 0,
            Message = requiredPaymentTotal == orderTotal
                ? "Full payment is required for in-stock items."
                : "Deposit is required for pre-order items."
        };
    }

    public async Task<string> CheckoutAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        var hasPaidDeposit = order.Payments.Any(x => x.PaymentPurpose == "DEPOSIT" && x.Status == "PAID");
        var purpose = (order.RemainingAmount ?? 0m) <= 0m
            ? "FULL"
            : hasPaidDeposit ? "REMAINING" : "DEPOSIT";

        var amount = purpose switch
        {
            "FULL" => order.TotalAmount ?? 0m,
            "DEPOSIT" => order.DepositAmount ?? 0m,
            _ => order.RemainingAmount ?? 0m
        };

        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            OrderId = order.Id,
            PaymentMethod = "VNPAY",
            PaymentPurpose = purpose,
            Amount = amount,
            Status = "UNPAID",
            Description = $"Payment for {purpose}"
        };

        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return $"https://sandbox.vnpay.vn/pay?paymentId={payment.Id}";
    }

    public async Task<string> HandleVnPayCallbackAsync(string? paymentId, string? transactionStatus, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return "/payment-failed";
        }

        var payment = await _dbContext.Payments.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            return "/payment-failed";
        }

        var success = transactionStatus == "00";
        payment.Status = success ? "PAID" : "FAILED";
        payment.PaymentDate = DateTime.UtcNow;

        if (success && payment.Order is not null)
        {
            if (payment.PaymentPurpose == "DEPOSIT")
            {
                payment.Order.PreOrderStatus = "DEPOSIT_PAID";
                payment.Order.Status = "AWAITING_VERIFICATION";
            }
            else if (payment.PaymentPurpose == "REMAINING")
            {
                payment.Order.PreOrderStatus = "REMAINING_PAID";
                payment.Order.Status = "PREPARING";
            }
            else
            {
                payment.Order.Status = "PREPARING";
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return success ? "/payment-success" : "/payment-failed";
    }

    public async Task<List<PaymentHistoryItemDto>> GetPaymentHistoryAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var data = await _dbContext.Payments
            .Include(x => x.Transactions)
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.PaymentDate)
            .ToListAsync(cancellationToken);

        return data.Select(x => new PaymentHistoryItemDto
        {
            Id = x.Id,
            PaymentMethod = x.PaymentMethod,
            PaymentPurpose = x.PaymentPurpose,
            Amount = x.Amount,
            Percentage = x.Percentage,
            Status = x.Status,
            PaymentDate = x.PaymentDate,
            Description = x.Description,
            TransactionReference = x.Transactions.FirstOrDefault()?.GatewayReference
        }).ToList();
    }
}
