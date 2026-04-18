using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Refunds;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class RefundWorkflowService : IRefundWorkflowService
{
    private readonly OpticalStoreDbContext _dbContext;

    public RefundWorkflowService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<object> InactivateVariantAsync(string variantId, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants.Include(x => x.Product).FirstOrDefaultAsync(x => x.Id == variantId && !(x.IsDeleted ?? false), cancellationToken);
        if (variant is null)
        {
            throw new AppException("PRODUCT_VARIANT_NOT_FOUND", "Variant not found.", HttpStatusCode.NotFound);
        }

        variant.Status = "INACTIVE";
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new
        {
            id = variant.Id,
            productId = variant.ProductId,
            colorName = variant.ColorName,
            frameFinish = variant.FrameFinish,
            lensWidthMm = variant.LensWidthMm,
            bridgeWidthMm = variant.BridgeWidthMm,
            templeLengthMm = variant.TempleLengthMm,
            sizeLabel = variant.SizeLabel,
            price = variant.Price,
            quantity = variant.Quantity,
            status = variant.Status,
            orderItemType = variant.OrderItemType
        };
    }

    public async Task<List<object>> GetAffectedOrdersAsync(string variantId, CancellationToken cancellationToken = default)
    {
        var orderIds = await _dbContext.OrderItems
            .Where(x => x.ProductVariantId == variantId && x.Order != null && x.Order.Status != "CANCELLED" && x.Order.Status != "REFUNDED")
            .Select(x => x.OrderId!)
            .Distinct()
            .ToListAsync(cancellationToken);

        var result = new List<object>();
        foreach (var orderId in orderIds)
        {
            result.Add(await BuildRefundResponse(orderId, cancellationToken));
        }

        return result;
    }

    public async Task<List<object>> CreateBatchAsync(RefundBatchDto request, CancellationToken cancellationToken = default)
    {
        var results = new List<object>();

        foreach (var orderId in request.OrderIds)
        {
            var order = await _dbContext.Orders.Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
            if (order is null)
            {
                continue;
            }

            var existed = await _dbContext.RefundRequests.AnyAsync(x => x.OrderId == orderId, cancellationToken);
            if (existed)
            {
                continue;
            }

            var paidAmount = order.Payments.Where(x => x.Status == "PAID").Sum(x => x.Amount ?? 0m);
            var refundPercentage = order.Status == "CANCELLED" ? 95m : 100m;
            var refundAmount = Math.Round(paidAmount * refundPercentage / 100m, 2, MidpointRounding.AwayFromZero);

            if (refundAmount <= 0m)
            {
                continue;
            }

            var refund = new RefundRequest
            {
                Id = Guid.NewGuid().ToString(),
                OrderId = order.Id,
                CustomerId = order.CustomerId,
                Status = "READY_FOR_REFUND",
                CreatedAt = DateTime.UtcNow,
                OrderTotalAmount = order.TotalAmount,
                RefundAmount = refundAmount,
                RefundPercentage = refundPercentage,
                DeductionAmount = paidAmount - refundAmount,
                BankName = order.BankName,
                BankAccountNumber = order.BankAccountNumber,
                AccountHolderName = order.AccountHolderName
            };

            _dbContext.RefundRequests.Add(refund);
            results.Add(await BuildRefundResponseFromEntity(refund, cancellationToken));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return results;
    }

    public async Task<List<object>> GetReadyAsync(CancellationToken cancellationToken = default)
    {
        var refunds = await _dbContext.RefundRequests.Include(x => x.Order).Where(x => x.Status == "READY_FOR_REFUND").ToListAsync(cancellationToken);
        var result = new List<object>();
        foreach (var refund in refunds)
        {
            result.Add(await BuildRefundResponseFromEntity(refund, cancellationToken));
        }

        return result;
    }

    public async Task<string> RefundCheckoutAsync(string refundId, CancellationToken cancellationToken = default)
    {
        var refund = await _dbContext.RefundRequests.FirstOrDefaultAsync(x => x.Id == refundId, cancellationToken);
        if (refund is null)
        {
            throw new AppException("REFUND_NOT_FOUND", "Refund not found.", HttpStatusCode.NotFound);
        }

        if (refund.Status is not ("READY_FOR_REFUND" or "FAILED"))
        {
            throw new AppException("INVALID_REFUND_STATUS", "Refund is not ready for checkout.", HttpStatusCode.BadRequest);
        }

        var amount = refund.RefundAmount ?? 0m;
        if (amount < 5000m)
        {
            throw new AppException("INVALID_PAYMENT_AMOUNT", "Refund amount must be at least 5,000.", HttpStatusCode.BadRequest);
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            OrderId = refund.OrderId,
            PaymentMethod = "VNPAY",
            PaymentPurpose = "REFUND",
            Amount = amount,
            Status = "UNPAID",
            Description = "Refund payment"
        };

        _dbContext.Payments.Add(payment);
        refund.PaymentId = payment.Id;
        refund.Status = "PROCESSING";

        await _dbContext.SaveChangesAsync(cancellationToken);

        return $"https://sandbox.vnpay.vn/refund?paymentId={payment.Id}";
    }

    private async Task<object> BuildRefundResponse(string orderId, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        var paidAmount = order.Payments.Where(x => x.Status == "PAID").Sum(x => x.Amount ?? 0m);
        var refundAmount = Math.Round(paidAmount * 0.95m, 2, MidpointRounding.AwayFromZero);

        return new
        {
            refundId = string.Empty,
            order = new
            {
                orderId = order.Id,
                customerId = order.CustomerId,
                status = order.Status,
                totalAmount = order.TotalAmount
            },
            refundAmount,
            refundPercentage = 95m,
            deductionAmount = paidAmount - refundAmount,
            refundStatus = "READY_FOR_REFUND"
        };
    }

    private async Task<object> BuildRefundResponseFromEntity(RefundRequest refund, CancellationToken cancellationToken)
    {
        var order = refund.Order ?? await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == refund.OrderId, cancellationToken);

        return new
        {
            refundId = refund.Id,
            order = order is null
                ? null
                : new
                {
                    orderId = order.Id,
                    customerId = order.CustomerId,
                    status = order.Status,
                    totalAmount = order.TotalAmount
                },
            refundAmount = refund.RefundAmount,
            refundPercentage = refund.RefundPercentage,
            deductionAmount = refund.DeductionAmount,
            refundStatus = refund.Status
        };
    }
}
