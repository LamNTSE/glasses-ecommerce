using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Orders;
using OpticalStore.BLL.DTOs.Notifications;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class OrdersWorkflowService : IOrdersWorkflowService
{
    private const decimal MaxDiscountPercent = 50m;
    private const decimal MinFinalPrice = 10000m;

    private readonly OpticalStoreDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public OrdersWorkflowService(OpticalStoreDbContext dbContext, INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    public async Task<object> CreateOrderAsync(CreateOrderDto request, string? paymentMethod, string userId, string? prescriptionImageFileName, CancellationToken cancellationToken = default)
    {
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

            var prescriptionId = await UpsertPrescription(null, item.Prescription, prescriptionImageFileName, cancellationToken);

            var unitPrice = variant.Price ?? 0m;
            var lensPrice = lens?.Price ?? 0m;
            var lineTotal = (unitPrice + lensPrice) * item.Quantity;
            total += lineTotal;

            var initialItemStatus = GetInitialOrderItemStatus(variant.OrderItemType, lens?.Id, prescriptionId);

            if (variant.Inventory is not null)
            {
                var available = (variant.Inventory.Quantity ?? 0) - (variant.Inventory.ReservedQuantity ?? 0);
                if (available < item.Quantity)
                {
                    throw new AppException("INSUFFICIENT_STOCK", "Not enough inventory available.", HttpStatusCode.BadRequest);
                }
                variant.Inventory.ReservedQuantity = (variant.Inventory.ReservedQuantity ?? 0) + item.Quantity;
                var avail = (variant.Inventory.Quantity ?? 0) - (variant.Inventory.ReservedQuantity ?? 0);
                variant.OrderItemType = avail > 0 ? "IN_STOCK" : "PRE_ORDER";
            }

            orderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid().ToString(),
                OrderId = order.Id,
                ProductVariantId = variant.Id,
                UnitPrice = unitPrice,
                LensPrice = lensPrice,
                LensId = lens?.Id,
                LensName = lens?.Name,
                OrderItemType = variant.OrderItemType,
                TotalPrice = lineTotal,
                Status = initialItemStatus,
                PrescriptionId = prescriptionId,
                DepositPrice = lineTotal,
                RemainingPrice = 0m,
                InventoryId = variant.Inventory?.Id
            });
        }

        var finalTotal = Math.Max(0m, total);
        var hasPreOrderItem = orderItems.Any(x => x.OrderItemType == "PRE_ORDER");
        var deposit = finalTotal;

        order.RequiresProduction = orderItems.Any(x => !string.IsNullOrWhiteSpace(x.LensId) || !string.IsNullOrWhiteSpace(x.PrescriptionId));
        order.OrderType = hasPreOrderItem ? "PREORDER" : "NORMAL";

        order.Status = "PENDING";

        order.TotalAmount = finalTotal;
        order.DepositAmount = deposit;
        order.RemainingAmount = 0m;
        order.PreOrderStatus = hasPreOrderItem ? "PREORDER_PENDING" : null;

        _dbContext.Orders.Add(order);
        _dbContext.OrderItems.AddRange(orderItems);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryCreateOrderNotificationAsync(customer.Id, "Đơn hàng đã được tạo", $"Đơn hàng {order.Id} đã được tạo thành công.", cancellationToken);

        return await BuildOrderResponse(order.Id, cancellationToken);
    }

    public async Task<PagedResultDto<object>> GetMyOrdersAsync(
        string userId,
        string? status,
        int page,
        int size,
        string sortBy,
        string sortDir,
        CancellationToken cancellationToken = default)
    {
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

        return new PagedResultDto<object>
        {
            Items = items,
            Page = safePage,
            Size = safeSize,
            TotalElements = totalElements,
            TotalPages = (int)Math.Ceiling(totalElements / (double)safeSize)
        };
    }

    public async Task<object> GetOrderByIdAsync(string orderId, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        if (!isAdmin && order.CustomerId != userId)
        {
            throw new AppException("FORBIDDEN", "You cannot access this order.", HttpStatusCode.Forbidden);
        }

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public async Task<object> UpdateOrderAsync(string orderId, UpdateOrderDto request, string userId, bool isAdmin, CancellationToken cancellationToken = default)
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

        if (!isAdmin && order.CustomerId != userId)
        {
            throw new AppException("FORBIDDEN", "You cannot update this order.", HttpStatusCode.Forbidden);
        }

        order.DeliveryAddress = request.DeliveryAddress ?? order.DeliveryAddress;
        order.RecipientName = request.RecipientName ?? order.RecipientName;
        order.PhoneNumber = request.PhoneNumber ?? order.PhoneNumber;

        // Recalculate RequiresProduction before CONFIRMED, lock after CONFIRMED
        if (order.Status is "PENDING")
        {
            order.RequiresProduction = order.OrderItems.Any(x => !string.IsNullOrWhiteSpace(x.LensId) || !string.IsNullOrWhiteSpace(x.PrescriptionId));
        }

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
            item.TotalPrice = ((item.UnitPrice ?? 0m) + (item.LensPrice ?? 0m)) * newQty;

            if (item.InventoryId is not null)
            {
                var inventory = await _dbContext.Inventories.FirstOrDefaultAsync(x => x.Id == item.InventoryId, cancellationToken);
                if (inventory is not null)
                {
                    var diff = newQty - oldQty;
                    // when increasing quantity, ensure enough available stock to reserve
                    if (diff > 0)
                    {
                        if (((inventory.ReservedQuantity ?? 0) + diff) > (inventory.Quantity ?? 0))
                        {
                            throw new AppException("INSUFFICIENT_STOCK", "Not enough inventory available to increase quantity.", HttpStatusCode.BadRequest);
                        }
                        inventory.ReservedQuantity = (inventory.ReservedQuantity ?? 0) + diff;
                    }
                    else if (diff < 0)
                    {
                        inventory.ReservedQuantity = Math.Max(0, (inventory.ReservedQuantity ?? 0) + diff);
                    }
                    var variantToUpdate = await _dbContext.ProductVariants.FirstOrDefaultAsync(x => x.Id == item.ProductVariantId, cancellationToken);
                    if (variantToUpdate is not null)
                    {
                        var avail = (inventory.Quantity ?? 0) - (inventory.ReservedQuantity ?? 0);
                        variantToUpdate.OrderItemType = avail > 0 ? "IN_STOCK" : "PRE_ORDER";
                    }
                }
            }

            if (update.Prescription is not null)
            {
                item.PrescriptionId = await UpsertPrescription(item.PrescriptionId, update.Prescription, null, cancellationToken);
            }
        }

        order.TotalAmount = order.OrderItems.Sum(x => x.TotalPrice ?? 0m);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public async Task<object> CancelOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders.Include(x => x.OrderItems).Include(x => x.Payments).FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        var cancelPermission = GetCancelPermission(order);
        if (!cancelPermission.canAutoCancel)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot be auto-cancelled in current state. Use request cancel instead.", HttpStatusCode.BadRequest);
        }

        var oldStatus = order.Status;
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
            var variantToUpdate = await _dbContext.ProductVariants.FirstOrDefaultAsync(x => x.Id == item.ProductVariantId, cancellationToken);
            if (variantToUpdate is not null)
            {
                var avail = (inventory.Quantity ?? 0) - (inventory.ReservedQuantity ?? 0);
                variantToUpdate.OrderItemType = avail > 0 ? "IN_STOCK" : "PRE_ORDER";
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogStatusTransitionAsync(order, oldStatus, "CANCELLED", order.CustomerId, "CUSTOMER", "API", null, "Customer cancelled order", cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public object GetCancelPermission(Order order)
    {
        var isPaid = order.Payments.Any(p => p.Status == "PAID");
        var productionStarted = order.Status == "IN_PRODUCTION";

        var canAutoCancel = !isPaid && !productionStarted && order.Status is "PENDING" or "CONFIRMED";
        var canRequestCancel = order.Status is "PENDING" or "CONFIRMED";

        return new
        {
            canAutoCancel,
            canRequestCancel
        };
    }

    public object GetAllowedActions(Order order, string? userRole)
    {
        var actions = new List<string>();

        // Edit action
        if (CanCustomerEdit(order.Status) && userRole == "CUSTOMER")
        {
            actions.Add("EDIT");
        }

        // Cancel actions
        var cancelPerm = GetCancelPermission(order);
        if (userRole == "CUSTOMER")
        {
            if (cancelPerm.canAutoCancel)
            {
                actions.Add("AUTO_CANCEL");
            }
            if (cancelPerm.canRequestCancel)
            {
                actions.Add("REQUEST_CANCEL");
            }
        }
        else if (userRole is "ADMIN" or "MANAGER" or "SALE")
        {
            actions.Add("CANCEL");
        }

        // Production actions (staff only)
        if (userRole is "OPERATION" or "ADMIN")
        {
            if (order.Status == "CONFIRMED")
            {
                if (order.RequiresProduction)
                {
                    actions.Add("START_PRODUCTION");
                }
                else
                {
                    actions.Add("MARK_READY_TO_SHIP");
                    actions.Add("MARK_WAITING_STOCK");
                }
            }

            if (order.Status == "IN_PRODUCTION")
            {
                actions.Add("FINISH_PRODUCTION");
            }

            if (order.Status == "WAITING_STOCK")
            {
                actions.Add("RECEIVE_STOCK");
            }

            if (order.Status == "READY_TO_SHIP")
            {
                actions.Add("START_DELIVERY");
            }
        }

        // Delivery actions (shipper/operation/admin)
        if (userRole is "SHIPPER" or "OPERATION" or "ADMIN")
        {
            if (order.Status == "DELIVERING")
            {
                actions.Add("CONFIRM_DELIVERED");
            }
        }

        // Verification actions (sale/admin/manager)
        if (userRole is "SALE" or "ADMIN" or "MANAGER")
        {
            if (order.Status == "PENDING")
            {
                actions.Add("VERIFY");
                actions.Add("REJECT");
            }
        }

        return new
        {
            allowedActions = actions
        };
    }

    // CompleteOrderAsync removed - DELIVERED is now the final state

    public async Task<object> UploadPrescriptionImageAsync(string orderItemId, string fileName, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.OrderItems.FirstOrDefaultAsync(x => x.Id == orderItemId, cancellationToken);
        if (item is null)
        {
            throw new AppException("ORDER_ITEM_NOT_FOUND", "Order item not found.", HttpStatusCode.NotFound);
        }

        var prescriptionId = await UpsertPrescription(item.PrescriptionId, new PrescriptionDto { ImageUrl = $"uploaded://{fileName}" }, fileName, cancellationToken);
        item.PrescriptionId = prescriptionId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var prescription = await _dbContext.Prescriptions.FirstAsync(x => x.Id == prescriptionId, cancellationToken);
        return MapPrescription(prescription);
    }

    public async Task<object> UpdatePrescriptionAsync(string orderItemId, PrescriptionDto request, CancellationToken cancellationToken = default)
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
        return MapPrescription(prescription);
    }

    public async Task<object> VerifyOrderAsync(string orderId, bool isApproved, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);

        if (!isApproved)
        {
            throw new AppException("WORKFLOW_NOT_SUPPORTED", "Reject verification is not supported in current lifecycle.", HttpStatusCode.BadRequest);
        }

        if (order.Status is not ("PENDING" or "AWAITING_VERIFICATION" or "PREORDER_PENDING"))
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot be verified in current status.", HttpStatusCode.BadRequest);
        }

        order.Status = order.Status == "PREORDER_PENDING" ? "PREORDER_CONFIRMED" : "CONFIRMED";
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đã được xác nhận",
            $"Đơn hàng {order.Id} đã được xác nhận và sẵn sàng cho sản xuất.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public Task<object> RevertVerifyOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        _ = orderId;
        _ = cancellationToken;
        return Task.FromException<object>(new AppException("WORKFLOW_NOT_SUPPORTED", "Revert verification is not supported in current lifecycle.", HttpStatusCode.BadRequest));
    }

    public Task<object> RejectOrderAsync(string orderId, string? reason, CancellationToken cancellationToken = default)
    {
        _ = orderId;
        _ = reason;
        _ = cancellationToken;
        return Task.FromException<object>(new AppException("WORKFLOW_NOT_SUPPORTED", "Reject order is not supported in current lifecycle.", HttpStatusCode.BadRequest));
    }

    public async Task<object> StartProductionAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);

        if (order.Status != "CONFIRMED")
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot start production in current status.", HttpStatusCode.BadRequest);
        }

        if (!order.RequiresProduction)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order does not require production.", HttpStatusCode.BadRequest);
        }

        var oldStatus = order.Status;
        order.Status = "IN_PRODUCTION";

        var items = await _dbContext.OrderItems.Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.Status = GetInitialOrderItemStatus(item.OrderItemType, item.LensId, item.PrescriptionId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogStatusTransitionAsync(order, oldStatus, "IN_PRODUCTION", null, "STAFF_OPERATION", "API", null, "Start production", cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đang sản xuất",
            $"Đơn hàng {order.Id} đã bắt đầu sản xuất.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public async Task<object> FinishProductionAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);

        if (order.Status != "IN_PRODUCTION")
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot finish production in current status.", HttpStatusCode.BadRequest);
        }

        var oldStatus = order.Status;
        order.Status = "READY_TO_SHIP";

        var items = await _dbContext.OrderItems.Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            // Only set PRODUCED for items that actually required production
            if (!string.IsNullOrWhiteSpace(item.LensId) || !string.IsNullOrWhiteSpace(item.PrescriptionId))
            {
                item.Status = "PRODUCED";
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogStatusTransitionAsync(order, oldStatus, "READY_TO_SHIP", null, "STAFF_OPERATION", "API", null, "Finish production", cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đã hoàn tất sản xuất",
            $"Đơn hàng {order.Id} đã hoàn tất sản xuất.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public async Task<object> MarkReadyToShipAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);

        if (order.Status != "CONFIRMED")
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot be marked ready to ship in current status.", HttpStatusCode.BadRequest);
        }

        if (order.RequiresProduction)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order requires production. Use StartProductionAsync instead.", HttpStatusCode.BadRequest);
        }

        var oldStatus = order.Status;
        order.Status = "READY_TO_SHIP";

        // Do NOT set item statuses to PRODUCED for frame-only orders (semantically wrong)

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogStatusTransitionAsync(order, oldStatus, "READY_TO_SHIP", null, "STAFF_OPERATION", "API", null, "Mark ready to ship", cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đã sẵn sàng giao",
            $"Đơn hàng {order.Id} đã sẵn sàng giao.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public async Task<object> MarkWaitingStockAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);

        if (order.Status != "CONFIRMED")
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot be marked waiting for stock in current status.", HttpStatusCode.BadRequest);
        }

        var oldStatus = order.Status;
        order.Status = "WAITING_STOCK";

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogStatusTransitionAsync(order, oldStatus, "WAITING_STOCK", null, "STAFF_OPERATION", "API", null, "Mark waiting for stock", cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đang chờ nhập hàng",
            $"Đơn hàng {order.Id} đang chờ nhập hàng.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public async Task<object> ReceiveStockAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);

        if (order.Status != "WAITING_STOCK")
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order is not in waiting stock status.", HttpStatusCode.BadRequest);
        }

        var oldStatus = order.Status;
        order.Status = "READY_TO_SHIP";

        await _dbContext.SaveChangesAsync(cancellationToken);

        await LogStatusTransitionAsync(order, oldStatus, "READY_TO_SHIP", null, "STAFF_OPERATION", "API", null, "Stock received", cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đã sẵn sàng giao",
            $"Đơn hàng {order.Id} đã sẵn sàng giao.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public Task<object> BulkReadyToShipAsync(IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken = default)
    {
        // Disabled for safety - bulk operations are dangerous and hard to rollback/audit
        // Use MarkReadyToShipAsync for individual orders instead
        _ = orderIds;
        _ = cancellationToken;
        return Task.FromException<object>(new AppException("WORKFLOW_NOT_SUPPORTED", "Bulk operations are disabled for safety. Use individual order actions instead.", HttpStatusCode.BadRequest));
    }

    public async Task<object> UpdateItemStatusAsync(string orderItemId, string status, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.OrderItems.FirstOrDefaultAsync(x => x.Id == orderItemId, cancellationToken);
        if (item is null)
        {
            throw new AppException("ORDER_ITEM_NOT_FOUND", "Order item not found.", HttpStatusCode.NotFound);
        }

        if (string.IsNullOrWhiteSpace(item.OrderId))
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found for this order item.", HttpStatusCode.NotFound);
        }

        var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == item.OrderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found for this order item.", HttpStatusCode.NotFound);
        }

        if (order.Status != "IN_PRODUCTION")
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order item status can only be updated when order is IN_PRODUCTION.", HttpStatusCode.BadRequest);
        }

        item.Status = status;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await BuildOrderResponse(item.OrderId, cancellationToken);
    }

    public async Task<object> StartDeliveryAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);

        if (order.Status != "READY_TO_SHIP")
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot start delivery in current status.", HttpStatusCode.BadRequest);
        }

        order.Status = "DELIVERING";
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đang giao",
            $"Đơn hàng {order.Id} đã được bàn giao cho bộ phận giao hàng.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public async Task<object> ConfirmDeliveredAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);

        if (order.Status != "DELIVERING")
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot be marked as delivered in current status.", HttpStatusCode.BadRequest);
        }

        order.Status = "DELIVERED";
        order.DeliveredAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đã giao thành công",
            $"Đơn hàng {order.Id} đã được giao thành công.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public Task<PagedResultDto<object>> GetCancelledPaidOrdersAsync(int page, int size, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        return GetManagementOrdersInternal("CANCELLED", onlyPaid: true, null, page, size, sortBy, sortDir, cancellationToken);
    }

    public async Task<object> GetManagementOrderByIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public Task<PagedResultDto<object>> GetManagementOrdersAsync(string? status, int page, int size, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        return GetManagementOrdersInternal(status, onlyPaid: false, null, page, size, sortBy, sortDir, cancellationToken);
    }

    public Task<PagedResultDto<object>> GetManagementOrdersByCustomerAsync(string customerId, int page, int size, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        return GetManagementOrdersInternal(null, onlyPaid: false, customerId, page, size, sortBy, sortDir, cancellationToken);
    }

    public async Task DeleteOrderLogicallyAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);
        order.Status = "DELETED";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<object> PriceCheckAsync(PriceCheckDto request, CancellationToken cancellationToken = default)
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

        const decimal comboDiscount = 0m;
        var warnings = new List<object>();

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

        return new
        {
            originalTotal,
            comboDiscount,
            finalTotal,
            isValid,
            itemDetails = detailRows,
            warnings
        };
    }

    private async Task<PagedResultDto<object>> GetManagementOrdersInternal(
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

        return new PagedResultDto<object>
        {
            Items = items,
            Page = safePage,
            Size = safeSize,
            TotalElements = totalElements,
            TotalPages = (int)Math.Ceiling(totalElements / (double)safeSize)
        };
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
            orderName = $"{(order.OrderItems.Any(i => string.Equals(i.OrderItemType, "PRE_ORDER", StringComparison.OrdinalIgnoreCase)) ? "PREORDER" : "ORDER")}-{order.Id[..Math.Min(8, order.Id.Length)]}",
            deliveryAddress = order.DeliveryAddress,
            recipientName = order.RecipientName,
            phoneNumber = order.PhoneNumber,
            paymentMethod = order.PaymentMethod,
            orderStatus = order.Status,
            totalAmount = order.TotalAmount,
            depositAmount = order.DepositAmount,
            remainingAmount = order.RemainingAmount,
            paidAmount = order.Payments.Where(x => x.Status == "PAID").Sum(x => x.Amount ?? 0m),
            requiresProduction = order.RequiresProduction,
            orderType = order.OrderType,
            items = itemResults,
            payments,
            bankInfo = new
            {
                bankName = order.BankName,
                bankAccountNumber = order.BankAccountNumber,
                accountHolderName = order.AccountHolderName
            }
        };
    }

    private async Task<string?> UpsertPrescription(string? prescriptionId, PrescriptionDto? request, string? fileName, CancellationToken cancellationToken)
    {
        if (request is null && string.IsNullOrWhiteSpace(fileName))
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

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            prescription.ImageUrl = $"uploaded://{fileName}";
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

    private static bool CanCustomerEdit(string? status)
    {
        return status is "PENDING";
    }

    private static bool CanCustomerCancel(string? status)
    {
        return status is "PENDING" or "CONFIRMED";
    }

    private static string GetInitialOrderItemStatus(string? orderItemType, string? lensId, string? prescriptionId)
    {
        var requiresProduction =
            string.Equals(orderItemType, "PRE_ORDER", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(lensId) ||
            !string.IsNullOrWhiteSpace(prescriptionId);

        return requiresProduction ? "IN_PRODUCTION" : "PRODUCED";
    }

    private async Task TryCreateOrderNotificationAsync(string recipientId, string title, string content, CancellationToken cancellationToken)
    {
        try
        {
            await _notificationService.CreateAsync("SYSTEM", new CreateNotificationDto
            {
                RecipientId = recipientId,
                Title = title,
                Content = content
            }, cancellationToken);
        }
        catch
        {
            // Notification is best-effort; order creation must still succeed.
        }
    }

    private async Task TryNotifyOrderCustomerAsync(Order order, string title, string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerId))
        {
            return;
        }

        await TryCreateOrderNotificationAsync(order.CustomerId, title, content, cancellationToken);
    }

    private async Task LogStatusTransitionAsync(Order order, string? oldStatus, string newStatus, string? userId, string? role, string source, string? ip = null, string? reason = null, CancellationToken cancellationToken = default)
    {
        var history = new OrderStatusHistory
        {
            Id = Guid.NewGuid().ToString(),
            OrderId = order.Id,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedAt = DateTime.UtcNow,
            ChangedByUserId = userId,
            ChangedByRole = role,
            Source = source,
            Ip = ip,
            Reason = reason
        };

        _dbContext.OrderStatusHistories.Add(history);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
