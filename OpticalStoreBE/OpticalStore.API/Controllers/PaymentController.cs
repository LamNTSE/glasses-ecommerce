using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpticalStore.API.Responses;
using OpticalStore.BLL.Exceptions;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("payment")]
[Tags("10. Payment")]
public sealed class PaymentController : ControllerBase
{
    private readonly OpticalStoreDbContext _dbContext;

    public PaymentController(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("orders/requirement")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetPaymentRequirement([FromBody] PaymentRequirementRequest request, CancellationToken cancellationToken)
    {
        var itemRequirements = new List<object>();
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

            itemRequirements.Add(new
            {
                orderItemId = (string?)null,
                orderItemType = variant?.OrderItemType ?? "IN_STOCK",
                quantity = item.Quantity,
                unitPrice,
                lensPrice,
                lensPriceTotal,
                baseItemTotal,
                itemTotal,
                paymentPercentage,
                requiredPayment
            });
        }

        var remaining = Math.Max(0m, orderTotal - requiredPaymentTotal);

        return Ok(new ApiResponse<object>
        {
            Result = new
            {
                depositPercentage = 0.5,
                requiredAmount = requiredPaymentTotal,
                orderTotal,
                requiredPaymentTotal,
                remainingPaymentTotal = remaining,
                itemRequirements,
                allowCOD = requiredPaymentTotal == 0,
                message = requiredPaymentTotal == orderTotal
                    ? "Full payment is required for in-stock items."
                    : "Deposit is required for pre-order items."
            }
        });
    }

    [HttpPost("checkout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<string>>> Checkout([FromQuery] string orderId, CancellationToken cancellationToken)
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

        var paymentUrl = $"https://sandbox.vnpay.vn/pay?paymentId={payment.Id}";
        return Ok(new ApiResponse<string> { Result = paymentUrl });
    }

    [HttpGet("vnpay-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayCallback(CancellationToken cancellationToken)
    {
        var paymentId = Request.Query["vnp_TxnRef"].ToString();
        var transactionStatus = Request.Query["vnp_TransactionStatus"].ToString();

        if (string.IsNullOrWhiteSpace(paymentId))
        {
            return Redirect("/payment-failed");
        }

        var payment = await _dbContext.Payments.Include(x => x.Order).FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            return Redirect("/payment-failed");
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
            else if (payment.PaymentPurpose == "REFUND")
            {
                payment.Order.Status = "REFUNDED";
            }
            else
            {
                payment.Order.Status = "PREPARING";
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Redirect(success ? "/payment-success" : "/payment-failed");
    }

    [HttpGet("orders/{orderId}/history")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetPaymentHistory(string orderId, CancellationToken cancellationToken)
    {
        var data = await _dbContext.Payments
            .Include(x => x.Transactions)
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.PaymentDate)
            .ToListAsync(cancellationToken);

        var result = data.Select(x => (object)new
        {
            id = x.Id,
            paymentMethod = x.PaymentMethod,
            paymentPurpose = x.PaymentPurpose,
            amount = x.Amount,
            percentage = x.Percentage,
            status = x.Status,
            paymentDate = x.PaymentDate,
            description = x.Description,
            transactionReference = x.Transactions.FirstOrDefault()?.GatewayReference
        }).ToList();

        return Ok(new ApiResponse<List<object>> { Result = result });
    }

    public sealed class PaymentRequirementRequest
    {
        public List<PaymentRequirementItemRequest> Items { get; set; } = new();
    }

    public sealed class PaymentRequirementItemRequest
    {
        public string? ProductVariantId { get; set; }

        public string? LensId { get; set; }

        public int Quantity { get; set; }
    }
}
