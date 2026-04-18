using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpticalStore.API.Requests.Orders;
using OpticalStore.API.Responses;
using OpticalStore.API.Swagger;
using OpticalStore.BLL.Exceptions;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.API.Controllers;

[ApiController]
[Tags("09. Orders")]
public sealed class OrdersWorkflowController : ControllerBase
{
    private const decimal MaxDiscountPercent = 50m;
    private const decimal MinFinalPrice = 10000m;

    private readonly OpticalStoreDbContext _dbContext;

    public OrdersWorkflowController(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost("orders/create")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    [SwaggerMultipartJsonPart("orderInfo", typeof(CreateOrderRequest))]
    public async Task<ActionResult<ApiResponse<object>>> CreateOrder(
        [FromForm] string orderInfo,
        [FromQuery(Name = "PaymentMethod")] string paymentMethod,
        IFormFile? prescriptionImage,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<CreateOrderRequest>(orderInfo, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new AppException("INVALID_PAYLOAD", "Invalid orderInfo payload.", HttpStatusCode.BadRequest);

        var userId = GetCurrentUserId();
        var customer = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (customer is null)
        {
            throw new AppException("USER_NOT_EXISTED", "User not found.", HttpStatusCode.NotFound);
        }

        var order = new Order
        {
            Id = Guid.NewGuid().ToString(),
            CustomerId = customer.Id,
            DeliveryAddress = request.DeliveryAddress,
            RecipientName = request.RecipientName,
            PhoneNumber = request.PhoneNumber,
            Status = "PENDING",
            CreatedAt = DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? null : paymentMethod,
            BankName = request.BankInfo?.BankName,
            BankAccountNumber = request.BankInfo?.BankAccountNumber,
            AccountHolderName = request.BankInfo?.AccountHolderName
        };

        decimal total = 0m;
        var orderItems = new List<OrderItem>();

        foreach (var item in request.Items)
        {
            var variant = await _dbContext.ProductVariants
                .Include(x => x.Product)
                .Include(x => x.Inventory)
                .FirstOrDefaultAsync(x => x.Id == item.ProductVariantId && !(x.IsDeleted ?? false), cancellationToken);

            if (variant is null)
            {
                throw new AppException("PRODUCT_VARIANT_NOT_FOUND", $"Variant {item.ProductVariantId} not found.", HttpStatusCode.NotFound);
            }

            if (item.Quantity < 1)
            {
                throw new AppException("INVALID_QUANTITY", "Quantity must be >= 1.", HttpStatusCode.BadRequest);
            }

            var lens = item.LensId is null
                ? null
                : await _dbContext.Lens.FirstOrDefaultAsync(x => x.Id == item.LensId && !x.IsDeleted, cancellationToken);

            var prescriptionId = await UpsertPrescription(null, item.Prescription, prescriptionImage, cancellationToken);

            var unitPrice = variant.Price ?? 0m;
            var lensPrice = lens?.Price ?? 0m;
            var lineTotal = (unitPrice + lensPrice) * item.Quantity;
            total += lineTotal;

            if (variant.Inventory is not null)
            {
                variant.Inventory.Quantity = Math.Max(0, (variant.Inventory.Quantity ?? 0) - item.Quantity);
                variant.Inventory.ReservedQuantity = (variant.Inventory.ReservedQuantity ?? 0) + item.Quantity;
            }

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid().ToString(),
                OrderId = order.Id,
                ProductVariantId = variant.Id,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                LensPrice = lensPrice,
                LensId = lens?.Id,
                LensName = lens?.Name,
                OrderItemType = variant.OrderItemType,
                TotalPrice = lineTotal,
                Status = "PENDING",
                PrescriptionId = prescriptionId,
                DepositPrice = variant.OrderItemType == "PRE_ORDER" ? (unitPrice * 0.5m + lensPrice) * item.Quantity : lineTotal,
                RemainingPrice = variant.OrderItemType == "PRE_ORDER" ? (unitPrice * 0.5m) * item.Quantity : 0m,
                InventoryId = variant.Inventory?.Id
            });
        }

        if (!string.IsNullOrWhiteSpace(request.ComboId))
        {
            var combo = await _dbContext.Combos.FirstOrDefaultAsync(x => x.Id == request.ComboId && !(x.IsDeleted ?? false), cancellationToken);
            if (combo is not null && combo.Status == "ACTIVE" && !combo.IsManuallyDisabled && combo.StartTime <= DateTime.UtcNow && combo.EndTime >= DateTime.UtcNow)
            {
                order.ComboId = combo.Id;
                order.ComboDiscountAmount = string.Equals(combo.DiscountType, "FIXED_AMOUNT", StringComparison.OrdinalIgnoreCase)
                    ? combo.DiscountValue
                    : Math.Round(total * combo.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero);
            }
        }

        var finalTotal = Math.Max(0m, total - (order.ComboDiscountAmount ?? 0m));
        var deposit = orderItems.Sum(x => x.DepositPrice ?? 0m);

        order.TotalAmount = finalTotal;
        order.DepositAmount = deposit;
        order.RemainingAmount = Math.Max(0m, finalTotal - deposit);
        order.PreOrderStatus = orderItems.Any(x => x.OrderItemType == "PRE_ORDER") ? "DEPOSIT_PENDING" : null;

        _dbContext.Orders.Add(order);
        _dbContext.OrderItems.AddRange(orderItems);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = await BuildOrderResponse(order.Id, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpGet("orders/me")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> GetMyOrders(
        [FromQuery] string? status,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var query = _dbContext.Orders.Where(x => x.CustomerId == userId);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status != null && x.Status.ToLower() == status.ToLower());
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sortBy.Trim().ToLower() switch
        {
            "totalamount" => desc ? query.OrderByDescending(x => x.TotalAmount) : query.OrderBy(x => x.TotalAmount),
            _ => desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
        };

        var safePage = Math.Max(0, page);
        var safeSize = Math.Clamp(size, 1, 200);
        var totalElements = await query.LongCountAsync(cancellationToken);
        var orders = await query.Skip(safePage * safeSize).Take(safeSize).Select(x => x.Id).ToListAsync(cancellationToken);

        var items = new List<object>();
        foreach (var orderId in orders)
        {
            items.Add(await BuildOrderResponse(orderId, cancellationToken));
        }

        return Ok(new ApiResponse<object>
        {
            Result = new
            {
                items,
                page = safePage,
                size = safeSize,
                totalElements,
                totalPages = (int)Math.Ceiling(totalElements / (double)safeSize)
            }
        });
    }

    [HttpGet("orders/{orderId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> GetOrderById(string orderId, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("ADMIN");
        if (!isAdmin && order.CustomerId != userId)
        {
            throw new AppException("FORBIDDEN", "You cannot access this order.", HttpStatusCode.Forbidden);
        }

        var result = await BuildOrderResponse(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpGet("orders/me/cancelled")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public Task<ActionResult<ApiResponse<object>>> GetMyCancelled(
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        return GetMyOrders("CANCELLED", page, size, sortBy, sortDir, cancellationToken);
    }

    [HttpPut("orders/{orderId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateOrder(string orderId, [FromBody] UpdateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        if (!CanCustomerEdit(order.Status))
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot be updated in current status.", HttpStatusCode.BadRequest);
        }

        var userId = GetCurrentUserId();
        var isAdmin = User.IsInRole("ADMIN");
        if (!isAdmin && order.CustomerId != userId)
        {
            throw new AppException("FORBIDDEN", "You cannot update this order.", HttpStatusCode.Forbidden);
        }

        order.DeliveryAddress = request.DeliveryAddress ?? order.DeliveryAddress;
        order.RecipientName = request.RecipientName ?? order.RecipientName;
        order.PhoneNumber = request.PhoneNumber ?? order.PhoneNumber;

        foreach (var update in request.Items)
        {
            var item = order.OrderItems.FirstOrDefault(x => x.Id == update.OrderItemId);
            if (item is null)
            {
                continue;
            }

            var oldQty = item.Quantity ?? 0;
            var newQty = Math.Max(1, update.Quantity);
            item.Quantity = newQty;
            item.TotalPrice = (item.UnitPrice ?? 0m + item.LensPrice ?? 0m) * newQty;

            if (item.InventoryId is not null)
            {
                var inventory = await _dbContext.Inventories.FirstOrDefaultAsync(x => x.Id == item.InventoryId, cancellationToken);
                if (inventory is not null)
                {
                    var diff = newQty - oldQty;
                    inventory.Quantity = (inventory.Quantity ?? 0) - diff;
                    inventory.ReservedQuantity = (inventory.ReservedQuantity ?? 0) + diff;
                }
            }

            if (update.Prescription is not null)
            {
                item.PrescriptionId = await UpsertPrescription(item.PrescriptionId, update.Prescription, null, cancellationToken);
            }
        }

        order.TotalAmount = order.OrderItems.Sum(x => x.TotalPrice ?? 0m) - (order.ComboDiscountAmount ?? 0m);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = await BuildOrderResponse(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("orders/{orderId}/cancel")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> CancelOrder(string orderId, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        if (!CanCustomerCancel(order.Status))
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot be cancelled in current status.", HttpStatusCode.BadRequest);
        }

        order.Status = "CANCELLED";

        foreach (var item in order.OrderItems)
        {
            if (item.InventoryId is null)
            {
                continue;
            }

            var inventory = await _dbContext.Inventories.FirstOrDefaultAsync(x => x.Id == item.InventoryId, cancellationToken);
            if (inventory is null)
            {
                continue;
            }

            inventory.ReservedQuantity = Math.Max(0, (inventory.ReservedQuantity ?? 0) - (item.Quantity ?? 0));
            inventory.Quantity = (inventory.Quantity ?? 0) + (item.Quantity ?? 0);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        var result = await BuildOrderResponse(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("orders/{orderId}/complete")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> CompleteOrder(string orderId, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        order.Status = "COMPLETED";
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = await BuildOrderResponse(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("orders/items/{orderItemId}/prescription-image")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> UploadPrescriptionImage(string orderItemId, IFormFile file, CancellationToken cancellationToken)
    {
        var item = await _dbContext.OrderItems.FirstOrDefaultAsync(x => x.Id == orderItemId, cancellationToken);
        if (item is null)
        {
            throw new AppException("ORDER_ITEM_NOT_FOUND", "Order item not found.", HttpStatusCode.NotFound);
        }

        var prescriptionId = await UpsertPrescription(item.PrescriptionId, new PrescriptionRequest { ImageUrl = $"uploaded://{file.FileName}" }, file, cancellationToken);
        item.PrescriptionId = prescriptionId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var prescription = await _dbContext.Prescriptions.FirstAsync(x => x.Id == prescriptionId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = MapPrescription(prescription) });
    }

    [HttpPut("orders/items/{orderItemId}/prescription")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePrescription(string orderItemId, [FromBody] PrescriptionRequest request, CancellationToken cancellationToken)
    {
        var item = await _dbContext.OrderItems.FirstOrDefaultAsync(x => x.Id == orderItemId, cancellationToken);
        if (item is null)
        {
            throw new AppException("ORDER_ITEM_NOT_FOUND", "Order item not found.", HttpStatusCode.NotFound);
        }

        var prescriptionId = await UpsertPrescription(item.PrescriptionId, request, null, cancellationToken);
        item.PrescriptionId = prescriptionId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var prescription = await _dbContext.Prescriptions.FirstAsync(x => x.Id == prescriptionId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = MapPrescription(prescription) });
    }

    [HttpPut("sales/orders/{orderId}/verify")]
    [Authorize(Roles = "SALE,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> VerifyOrder(string orderId, [FromQuery] bool isApproved, CancellationToken cancellationToken)
    {
        var order = await GetOrder(orderId, cancellationToken);
        order.Status = isApproved ? "PROCESSING" : "ON_HOLD";
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object> { Result = await BuildOrderResponse(orderId, cancellationToken) });
    }

    [HttpPut("sales/orders/{orderId}/revert-verify")]
    [Authorize(Roles = "SALE,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> RevertVerifyOrder(string orderId, CancellationToken cancellationToken)
    {
        var order = await GetOrder(orderId, cancellationToken);
        order.Status = "AWAITING_VERIFICATION";
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object> { Result = await BuildOrderResponse(orderId, cancellationToken) });
    }

    [HttpPut("sales/orders/{orderId}/reject")]
    [Authorize(Roles = "SALE,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> RejectOrder(string orderId, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        _ = reason;
        var order = await GetOrder(orderId, cancellationToken);
        order.Status = "ON_HOLD";
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object> { Result = await BuildOrderResponse(orderId, cancellationToken) });
    }

    [HttpPut("production/orders/{orderId}/start")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> StartProduction(string orderId, CancellationToken cancellationToken)
    {
        var order = await GetOrder(orderId, cancellationToken);
        order.Status = "PROCESSING";

        var items = await _dbContext.OrderItems.Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.Status = "IN_PRODUCTION";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object> { Result = await BuildOrderResponse(orderId, cancellationToken) });
    }

    [HttpPut("production/orders/{orderId}/finish")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> FinishProduction(string orderId, CancellationToken cancellationToken)
    {
        var order = await GetOrder(orderId, cancellationToken);
        order.Status = "PRODUCED";

        var items = await _dbContext.OrderItems.Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.Status = "PRODUCED";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<object> { Result = await BuildOrderResponse(orderId, cancellationToken) });
    }

    [HttpPut("production/orders/items/{orderItemId}/status")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateItemStatus(string orderItemId, [FromQuery] string status, CancellationToken cancellationToken)
    {
        var item = await _dbContext.OrderItems.FirstOrDefaultAsync(x => x.Id == orderItemId, cancellationToken);
        if (item is null)
        {
            throw new AppException("ORDER_ITEM_NOT_FOUND", "Order item not found.", HttpStatusCode.NotFound);
        }

        item.Status = status;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var orderId = item.OrderId ?? string.Empty;
        return Ok(new ApiResponse<object> { Result = await BuildOrderResponse(orderId, cancellationToken) });
    }

    [HttpPatch("ship/orders/accept")]
    [Authorize(Roles = "SHIPPER,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<object>>>> AcceptShipOrders([FromBody] AcceptShipOrdersRequest request, CancellationToken cancellationToken)
    {
        var shipperId = GetCurrentUserId();
        var result = new List<object>();

        foreach (var orderId in request.OrderIds)
        {
            var order = await GetOrder(orderId, cancellationToken);
            order.Status = "SHIPPED";
            order.ShipperId = shipperId;
            order.ShippedAt = DateTime.UtcNow;
            result.Add(await BuildOrderResponse(orderId, cancellationToken));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new ApiResponse<List<object>> { Result = result });
    }

    [HttpGet("ship/orders/my-orders-accepted")]
    [Authorize(Roles = "SHIPPER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> GetMyAcceptedShipOrders(
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var shipperId = GetCurrentUserId();
        var query = _dbContext.Orders.Where(x => x.ShipperId == shipperId && (x.Status == "SHIPPED" || x.Status == "DELIVERING"));

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sortBy.Trim().ToLower() switch
        {
            "totalamount" => desc ? query.OrderByDescending(x => x.TotalAmount) : query.OrderBy(x => x.TotalAmount),
            _ => desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
        };

        var safePage = Math.Max(0, page);
        var safeSize = Math.Clamp(size, 1, 200);
        var totalElements = await query.LongCountAsync(cancellationToken);
        var orderIds = await query.Skip(safePage * safeSize).Take(safeSize).Select(x => x.Id).ToListAsync(cancellationToken);

        var items = new List<object>();
        foreach (var orderId in orderIds)
        {
            items.Add(await BuildOrderResponse(orderId, cancellationToken));
        }

        return Ok(new ApiResponse<object>
        {
            Result = new
            {
                items,
                page = safePage,
                size = safeSize,
                totalElements,
                totalPages = (int)Math.Ceiling(totalElements / (double)safeSize)
            }
        });
    }

    [HttpPatch("ship/orders/{orderId}/start-delivery")]
    [Authorize(Roles = "SHIPPER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> StartDelivery(string orderId, CancellationToken cancellationToken)
    {
        var shipperId = GetCurrentUserId();
        var order = await GetOrder(orderId, cancellationToken);

        if (order.ShipperId != shipperId)
        {
            throw new AppException("FORBIDDEN", "Order is not assigned to current shipper.", HttpStatusCode.Forbidden);
        }

        order.Status = "DELIVERING";
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object> { Result = await BuildOrderResponse(orderId, cancellationToken) });
    }

    [HttpPatch("ship/orders/{orderId}/confirm-delivered")]
    [Authorize(Roles = "SHIPPER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmDelivered(string orderId, CancellationToken cancellationToken)
    {
        var shipperId = GetCurrentUserId();
        var order = await GetOrder(orderId, cancellationToken);

        if (order.ShipperId != shipperId)
        {
            throw new AppException("FORBIDDEN", "Order is not assigned to current shipper.", HttpStatusCode.Forbidden);
        }

        order.Status = "DELIVERED";
        order.DeliveredAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object> { Result = await BuildOrderResponse(orderId, cancellationToken) });
    }

    [HttpGet("management/orders/cancelled/paid")]
    [Authorize(Roles = "MANAGER,ADMIN,SALE,OPERATION,SHIPPER")]
    public Task<ActionResult<ApiResponse<object>>> GetCancelledPaidOrders(
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        return GetManagementOrders("CANCELLED", onlyPaid: true, null, page, size, sortBy, sortDir, cancellationToken);
    }

    [HttpGet("management/orders/{orderId}")]
    [Authorize(Roles = "MANAGER,ADMIN,SALE,OPERATION,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> GetManagementOrderById(string orderId, CancellationToken cancellationToken)
    {
        return Ok(new ApiResponse<object> { Result = await BuildOrderResponse(orderId, cancellationToken) });
    }

    [HttpGet("management/orders")]
    [Authorize(Roles = "MANAGER,ADMIN,SALE,OPERATION,SHIPPER")]
    public Task<ActionResult<ApiResponse<object>>> GetManagementOrders(
        [FromQuery] string? status,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        return GetManagementOrders(status, false, null, page, size, sortBy, sortDir, cancellationToken);
    }

    [HttpGet("management/orders/customer/{customerId}")]
    [Authorize(Roles = "MANAGER,ADMIN,SALE,OPERATION,SHIPPER")]
    public Task<ActionResult<ApiResponse<object>>> GetManagementOrdersByCustomer(
        string customerId,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        return GetManagementOrders(null, false, customerId, page, size, sortBy, sortDir, cancellationToken);
    }

    [HttpDelete("management/orders/{orderId}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteOrderLogically(string orderId, CancellationToken cancellationToken)
    {
        var order = await GetOrder(orderId, cancellationToken);
        order.Status = "DELETED";
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Message = "Order deleted successfully from system logs",
            Result = null
        });
    }

    [HttpPost("api/orders/price-check")]
    [Authorize(Roles = "SALE,ADMIN,OPERATION")]
    public async Task<ActionResult<ApiResponse<object>>> PriceCheck([FromBody] PriceCheckRequest request, CancellationToken cancellationToken)
    {
        var detailRows = new List<object>();
        decimal originalTotal = 0m;

        foreach (var item in request.Items)
        {
            var variant = await _dbContext.ProductVariants
                .Include(x => x.Product)
                .FirstOrDefaultAsync(x => x.Id == item.ProductVariantId && !(x.IsDeleted ?? false), cancellationToken);

            if (variant is null)
            {
                throw new AppException("PRODUCT_VARIANT_NOT_FOUND", "Variant not found.", HttpStatusCode.NotFound);
            }

            var lineTotal = (variant.Price ?? 0m) * item.Quantity;
            originalTotal += lineTotal;

            detailRows.Add(new
            {
                productVariantId = variant.Id,
                productName = variant.Product.Name,
                skuLabel = $"{variant.ColorName}-{variant.SizeLabel}",
                quantity = item.Quantity,
                unitPrice = variant.Price ?? 0m,
                lineTotal
            });
        }

        decimal comboDiscount = 0m;
        var warnings = new List<object>();

        if (!string.IsNullOrWhiteSpace(request.ComboId))
        {
            var combo = await _dbContext.Combos.Include(x => x.ComboItems).ThenInclude(x => x.ProductVariant)
                .FirstOrDefaultAsync(x => x.Id == request.ComboId && !(x.IsDeleted ?? false), cancellationToken);

            if (combo is null)
            {
                warnings.Add(new { type = "COMBO_NOT_FOUND", message = "Combo not found", threshold = 0m, actualValue = 0m });
            }
            else if (combo.Status != "ACTIVE" || combo.IsManuallyDisabled || combo.StartTime > DateTime.UtcNow || combo.EndTime < DateTime.UtcNow)
            {
                warnings.Add(new { type = "COMBO_NOT_ACTIVE", message = "Combo is not active", threshold = 0m, actualValue = 0m });
            }
            else
            {
                comboDiscount = string.Equals(combo.DiscountType, "FIXED_AMOUNT", StringComparison.OrdinalIgnoreCase)
                    ? combo.DiscountValue
                    : Math.Round(originalTotal * combo.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero);
            }
        }

        var finalTotal = Math.Max(0m, originalTotal - comboDiscount);
        var isValid = true;

        var discountPercent = originalTotal == 0 ? 0 : (comboDiscount / originalTotal) * 100m;
        if (discountPercent > MaxDiscountPercent)
        {
            isValid = false;
            warnings.Add(new
            {
                type = "DISCOUNT_EXCEEDS_THRESHOLD",
                message = "Discount exceeds threshold",
                threshold = MaxDiscountPercent,
                actualValue = discountPercent
            });
        }

        if (finalTotal < MinFinalPrice && originalTotal > 0)
        {
            isValid = false;
            warnings.Add(new
            {
                type = "BELOW_MIN_PRICE",
                message = "Final total is below minimum price",
                threshold = MinFinalPrice,
                actualValue = finalTotal
            });
        }

        return Ok(new ApiResponse<object>
        {
            Message = "Kiểm tra giá thành công",
            Result = new
            {
                originalTotal,
                comboDiscount,
                finalTotal,
                isValid,
                itemDetails = detailRows,
                warnings
            }
        });
    }

    private async Task<ActionResult<ApiResponse<object>>> GetManagementOrders(
        string? status,
        bool onlyPaid,
        string? customerId,
        int page,
        int size,
        string sortBy,
        string sortDir,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Orders.AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status != null && x.Status.ToLower() == status.ToLower());
        }

        if (!string.IsNullOrWhiteSpace(customerId))
        {
            query = query.Where(x => x.CustomerId == customerId);
        }

        if (onlyPaid)
        {
            query = query.Where(x => x.Status == "CANCELLED" && x.Payments.Any(p => p.Status == "PAID"));
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sortBy.Trim().ToLower() switch
        {
            "totalamount" => desc ? query.OrderByDescending(x => x.TotalAmount) : query.OrderBy(x => x.TotalAmount),
            _ => desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
        };

        var safePage = Math.Max(0, page);
        var safeSize = Math.Clamp(size, 1, 200);
        var totalElements = await query.LongCountAsync(cancellationToken);
        var orderIds = await query.Skip(safePage * safeSize).Take(safeSize).Select(x => x.Id).ToListAsync(cancellationToken);

        var items = new List<object>();
        foreach (var orderId in orderIds)
        {
            items.Add(await BuildOrderResponse(orderId, cancellationToken));
        }

        return Ok(new ApiResponse<object>
        {
            Result = new
            {
                items,
                page = safePage,
                size = safeSize,
                totalElements,
                totalPages = (int)Math.Ceiling(totalElements / (double)safeSize)
            }
        });
    }

    private async Task<Order> GetOrder(string orderId, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        return order;
    }

    private async Task<object> BuildOrderResponse(string orderId, CancellationToken cancellationToken)
    {
#pragma warning disable CS8602
        var order = await _dbContext.Orders
            .Include(x => x.Customer)
            .Include(x => x.Combo)
            .Include(x => x.Payments)
            .Include(x => x.OrderItems)
                .ThenInclude(x => x.ProductVariant)
                    .ThenInclude(x => x.Product!)
            .Include(x => x.OrderItems)
                .ThenInclude(x => x.Prescription)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
#pragma warning restore CS8602

        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        var itemResults = order.OrderItems.Select(item => new
        {
            orderItemId = item.Id,
            productId = item.ProductVariant?.ProductId,
            productVariantId = item.ProductVariantId,
            itemName = item.ProductVariant?.Product?.Name,
            productName = item.ProductVariant?.Product?.Name,
            productImage = item.ProductVariant?.Product?.ProductImages.FirstOrDefault()?.ImageUrl,
            variantName = item.ProductVariant is null ? null : $"{item.ProductVariant.ColorName}-{item.ProductVariant.SizeLabel}",
            orderItemType = item.OrderItemType,
            quantity = item.Quantity,
            unitPrice = item.UnitPrice,
            lensId = item.LensId,
            lensName = item.LensName,
            lensPrice = item.LensPrice,
            lensPriceTotal = (item.LensPrice ?? 0m) * (item.Quantity ?? 0),
            totalPrice = item.TotalPrice,
            status = item.Status,
            prescription = item.Prescription is null ? null : MapPrescription(item.Prescription)
        }).ToList();

        var payments = order.Payments.OrderByDescending(x => x.PaymentDate).Select(x => new
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

        return new
        {
            customerId = order.CustomerId,
            orderId = order.Id,
            orderName = $"ORDER-{order.Id[..Math.Min(8, order.Id.Length)]}",
            deliveryAddress = order.DeliveryAddress,
            recipientName = order.RecipientName,
            phoneNumber = order.PhoneNumber,
            orderStatus = order.Status,
            totalAmount = order.TotalAmount,
            depositAmount = order.DepositAmount,
            remainingAmount = order.RemainingAmount,
            paidAmount = order.Payments.Where(x => x.Status == "PAID").Sum(x => x.Amount ?? 0m),
            items = itemResults,
            payments,
            shipperInfo = order.ShipperId is null ? null : new { shipperId = order.ShipperId },
            comboId = order.ComboId,
            comboName = order.Combo?.Name,
            comboDiscountAmount = order.ComboDiscountAmount,
            comboSnapshot = order.ComboSnapshot,
            refundedAmount = order.RefundRequests.Sum(x => x.RefundAmount ?? 0m),
            finalTotalAfterRefund = (order.TotalAmount ?? 0m) - order.RefundRequests.Sum(x => x.RefundAmount ?? 0m),
            bankInfo = new
            {
                bankName = order.BankName,
                bankAccountNumber = order.BankAccountNumber,
                accountHolderName = order.AccountHolderName
            }
        };
    }

    private async Task<string?> UpsertPrescription(string? prescriptionId, PrescriptionRequest? request, IFormFile? file, CancellationToken cancellationToken)
    {
        if (request is null && file is null)
        {
            return prescriptionId;
        }

        Prescription? prescription = null;
        if (!string.IsNullOrWhiteSpace(prescriptionId))
        {
            prescription = await _dbContext.Prescriptions.FirstOrDefaultAsync(x => x.Id == prescriptionId, cancellationToken);
        }

        if (prescription is null)
        {
            prescription = new Prescription { Id = Guid.NewGuid().ToString() };
            _dbContext.Prescriptions.Add(prescription);
        }

        if (request is not null)
        {
            prescription.ImageUrl = request.ImageUrl ?? prescription.ImageUrl;
            prescription.OdSphere = request.OdSphere;
            prescription.OdCylinder = request.OdCylinder;
            prescription.OdAxis = request.OdAxis;
            prescription.OdAdd = request.OdAdd;
            prescription.OdPd = request.OdPd;
            prescription.OsSphere = request.OsSphere;
            prescription.OsCylinder = request.OsCylinder;
            prescription.OsAxis = request.OsAxis;
            prescription.OsAdd = request.OsAdd;
            prescription.OsPd = request.OsPd;
            prescription.Note = request.Note;
        }

        if (file is not null)
        {
            prescription.ImageUrl = $"uploaded://{file.FileName}";
        }

        return prescription.Id;
    }

    private static object MapPrescription(Prescription prescription)
    {
        return new
        {
            id = prescription.Id,
            imageUrl = prescription.ImageUrl,
            odSphere = prescription.OdSphere,
            odCylinder = prescription.OdCylinder,
            odAxis = prescription.OdAxis,
            odAdd = prescription.OdAdd,
            odPd = prescription.OdPd,
            osSphere = prescription.OsSphere,
            osCylinder = prescription.OsCylinder,
            osAxis = prescription.OsAxis,
            osAdd = prescription.OsAdd,
            osPd = prescription.OsPd,
            note = prescription.Note
        };
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue("userId")
            ?? throw new AppException("UNAUTHENTICATED", "Missing userId claim.", HttpStatusCode.Unauthorized);
    }

    private static bool CanCustomerEdit(string? status)
    {
        return status is "PENDING" or "ON_HOLD";
    }

    private static bool CanCustomerCancel(string? status)
    {
        return status is "PENDING" or "PREPARING" or "AWAITING_VERIFICATION" or "ON_HOLD";
    }

}
