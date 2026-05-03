using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpticalStore.BLL;
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
    private const string StatusPending = "PENDING";
    private const string StatusPaid = "PAID";
    private const string StatusConfirmed = "CONFIRMED";
    private const string StatusPreOrderConfirmed = "PREORDER_CONFIRMED";
    private const string StatusStockRequested = "STOCK_REQUESTED";
    private const string StatusStockReady = "STOCK_READY";
    private const string StatusInProduction = "IN_PRODUCTION";
    private const string StatusReadyToShip = "READY_TO_SHIP";
    private const string StatusDelivering = "DELIVERING";
    private const string StatusDelivered = "DELIVERED";
    private const string StatusOnHold = "ON_HOLD";
    private readonly OpticalStoreDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<OrdersWorkflowService> _logger;

    // Khoi tao service don hang va gan cac dependency can thiet.
    public OrdersWorkflowService(
        OpticalStoreDbContext dbContext,
        INotificationService notificationService,
        ILogger<OrdersWorkflowService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    // Tao don hang moi va khoi tao cac order item lien quan.
    public async Task<object> CreateOrderAsync(CreateOrderDto request, string? paymentMethod, string userId, string? prescriptionImageRelativeUrl, CancellationToken cancellationToken = default)
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
            CreatedAt = DateTime.UtcNow,
            PaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? null : paymentMethod,
            BankName = request.BankInfo?.BankName,
            BankAccountNumber = request.BankInfo?.BankAccountNumber,
            AccountHolderName = request.BankInfo?.AccountHolderName
        };

        decimal total = 0m;
        var orderItems = new List<OrderItem>();
        var orderItemIndex = 0;
        // Một file upload cho cả đơn: ưu tiên gắn prescription vào dòng đầu có tròng; không thì dòng đầu
        var itemsList = request.Items.ToList();
        var uploadTargetIndex = itemsList.FindIndex(x => !string.IsNullOrWhiteSpace(x.LensId));
        if (uploadTargetIndex < 0)
        {
            uploadTargetIndex = 0;
        }

        // Duyet tung item de tinh gia, gan prescription va cap nhat ton kho.
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

            var lineImage = orderItemIndex == uploadTargetIndex && !string.IsNullOrWhiteSpace(prescriptionImageRelativeUrl)
                ? prescriptionImageRelativeUrl
                : null;

            var prescriptionId = await UpsertPrescription(null, item.Prescription, lineImage, cancellationToken);

            var unitPrice = variant.Price ?? 0m;
            var lensPrice = lens?.Price ?? 0m;
            var lineTotal = (unitPrice + lensPrice) * item.Quantity;
            total += lineTotal;

            var orderItemType = ResolveOrderItemType(variant);
            var initialItemStatus = GetInitialOrderItemStatus(orderItemType, lens?.Id, prescriptionId);

            if (variant.Inventory is not null)
            {
                // Chi tru kho khi item nam trong nhom IN_STOCK.
                if (string.Equals(orderItemType, "IN_STOCK", StringComparison.OrdinalIgnoreCase))
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
                else
                {
                    variant.OrderItemType = "PRE_ORDER";
                }
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
                OrderItemType = orderItemType,
                TotalPrice = lineTotal,
                Status = initialItemStatus,
                PrescriptionId = prescriptionId,
                DepositPrice = 0m,
                RemainingPrice = lineTotal,
                InventoryId = variant.Inventory?.Id
            });
            orderItemIndex++;
        }

        var finalTotal = Math.Max(0m, total);
        var hasPreOrderItems = orderItems.Any(x => IsPreOrderOrderItemType(x.OrderItemType));

        if (hasPreOrderItems && !string.Equals(paymentMethod?.Trim(), "VNPAY", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException(
                "PREORDER_PAYMENT_METHOD_NOT_ALLOWED",
                "Pre-order orders must be paid 100% via VNPay.",
                HttpStatusCode.BadRequest);
        }

        order.TotalAmount = finalTotal;
        order.DepositAmount = 0m;
        order.RemainingAmount = finalTotal;
        order.Status = StatusPending;
        order.PreOrderStatus = null;

        _dbContext.Orders.Add(order);
        _dbContext.OrderItems.AddRange(orderItems);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryCreateOrderNotificationAsync(customer.Id, "Đơn hàng đã được tạo", $"Đơn hàng {order.Id} đã được tạo thành công.", cancellationToken);

        return await BuildOrderResponse(order.Id, cancellationToken);
    }

    // Lay danh sach don hang cua mot khach va phan trang.
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
            query = status.Trim().ToUpperInvariant() switch
            {
                StatusPreOrderConfirmed => query.Where(x => x.Status == StatusPreOrderConfirmed),
                StatusConfirmed => query.Where(x => x.Status == StatusConfirmed),
                _ => query.Where(x => x.Status != null && x.Status.ToLower() == status.ToLower())
            };
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sortBy.Trim().ToLower() switch
        {
            "totalamount" => desc
                ? query.OrderByDescending(x => x.TotalAmount).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.TotalAmount).ThenBy(x => x.Id),
            _ => desc
                ? query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
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

    // Lay chi tiet mot don hang va kiem tra quyen truy cap.
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

    // Cap nhat don hang khi khach con duoc phep sua.
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

        // Cap nhat tung order item va dieu chinh reserved quantity neu can.
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

            if (item.InventoryId is not null && IsInStockLine(item.OrderItemType))
            {
                var inventory = await _dbContext.Inventories.FirstOrDefaultAsync(x => x.Id == item.InventoryId, cancellationToken);
                if (inventory is not null)
                {
                    var diff = newQty - oldQty;
                    // when increasing quantity, ensure enough available stock to reserve
                        // Neu tang so luong thi phai dam bao ton kho du de giu cho.
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

    // Huy don hang va tra lai ton kho neu co.
    public async Task<object> CancelOrderAsync(string orderId, string? cancellationReason, string cancelledByRole, CancellationToken cancellationToken = default)
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
        ApplyCancellationMetadata(order, cancellationReason, cancelledByRole);

        // Hoan tra reserved quantity cho cac dong hang IN_STOCK.
        foreach (var item in order.OrderItems)
        {
            if (item.InventoryId is null || !IsInStockLine(item.OrderItemType))
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

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đã bị hủy",
            $"Đơn hàng {order.Id} đã được hủy thành công.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    // Xac nhan hoan tat don hang sau khi da giao.
    public async Task<object> CompleteOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        if (order.Status != "DELIVERED")
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot be completed in current status.", HttpStatusCode.BadRequest);
        }

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    // Gan hoac cap nhat anh prescription cho order item.
    public async Task<object> UploadPrescriptionImageAsync(string orderItemId, string prescriptionImageRelativeUrl, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.OrderItems.FirstOrDefaultAsync(x => x.Id == orderItemId, cancellationToken);
        if (item is null)
        {
            throw new AppException("ORDER_ITEM_NOT_FOUND", "Order item not found.", HttpStatusCode.NotFound);
        }

        var prescriptionId = await UpsertPrescription(item.PrescriptionId, null, prescriptionImageRelativeUrl, cancellationToken);
        item.PrescriptionId = prescriptionId;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var prescription = await _dbContext.Prescriptions.FirstAsync(x => x.Id == prescriptionId, cancellationToken);
        return MapPrescription(prescription);
    }

    // Cap nhat prescription cua order item.
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

    // Duyet don hang va chuyen sang trang thai tiep theo.
    public async Task<object> VerifyOrderAsync(string orderId, bool isApproved, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);
        EnsureNotOnOperationalHold(order);

        if (!isApproved)
        {
            throw new AppException("WORKFLOW_NOT_SUPPORTED", "Reject verification is not supported in current lifecycle.", HttpStatusCode.BadRequest);
        }

        var hasPreOrderItems = await _dbContext.OrderItems.AnyAsync(
            x => x.OrderId == orderId
                && x.OrderItemType != null
                && (x.OrderItemType.ToUpper() == "PRE_ORDER" || x.OrderItemType.ToUpper() == "PREORDER"),
            cancellationToken);
        var canVerify = hasPreOrderItems
            ? order.Status is StatusPaid or "AWAITING_VERIFICATION"
            : order.Status is StatusPending or StatusPaid or "AWAITING_VERIFICATION";

        if (!canVerify)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot be verified in current status.", HttpStatusCode.BadRequest);
        }

        order.Status = hasPreOrderItems ? StatusPreOrderConfirmed : StatusConfirmed;
        order.PreOrderStatus = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var statusSummary = hasPreOrderItems
            ? "Đã xác nhận preorder — đơn sẵn sàng chuyển xử lý kế tiếp."
            : "Đã xác nhận — đơn sẵn sàng cho sản xuất / thao tác tiếp theo.";

        await TryNotifyOrderCustomerAsync(
            order,
            "Cập nhật trạng thái đơn hàng",
            $"Người bán vừa cập nhật đơn {order.Id}. {statusSummary}",
            cancellationToken);

        return new
        {
            orderId = order.Id,
            orderStatus = GetDisplayStatus(order.Status, hasPreOrderItems, order.PreOrderStatus),
            preOrderStatus = order.PreOrderStatus
        };
    }

    // Lua chon quay lui verify khong duoc ho tro trong lifecycle hien tai.
    public Task<object> RevertVerifyOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        _ = orderId;
        _ = cancellationToken;
        return Task.FromException<object>(new AppException("WORKFLOW_NOT_SUPPORTED", "Revert verification is not supported in current lifecycle.", HttpStatusCode.BadRequest));
    }

    // Tu choi don hang o buoc ban hang.
    public Task<object> RejectOrderAsync(string orderId, string? reason, string cancelledByRole, CancellationToken cancellationToken = default)
    {
        return RejectOrderInternalAsync(orderId, reason, cancelledByRole, cancellationToken);
    }

    // Yeu cau nhap kho cho don preorder.
    public async Task<object> RequestStockAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);
        EnsureNotOnOperationalHold(order);
        var hasPreOrderItems = await _dbContext.OrderItems.AnyAsync(
            x => x.OrderId == orderId
                && x.OrderItemType != null
                && (x.OrderItemType.ToUpper() == "PRE_ORDER" || x.OrderItemType.ToUpper() == "PREORDER"),
            cancellationToken);

        if (!hasPreOrderItems)
        {
            throw new AppException("ORDER_NOT_PREORDER", "Only pre-order orders can request stock.", HttpStatusCode.BadRequest);
        }

        if (order.Status != StatusPreOrderConfirmed)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Stock can only be requested after sale confirmation (PREORDER_CONFIRMED).", HttpStatusCode.BadRequest);
        }

        order.Status = StatusStockRequested;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đang chờ nhập hàng",
            $"Đơn hàng {order.Id} đang được yêu cầu nhập hàng.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    // Danh dau hang da san sang sau khi da yeu cau nhap kho.
    public async Task<object> MarkStockReadyAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);
        EnsureNotOnOperationalHold(order);
        var hasPreOrderItems = await _dbContext.OrderItems.AnyAsync(
            x => x.OrderId == orderId
                && x.OrderItemType != null
                && (x.OrderItemType.ToUpper() == "PRE_ORDER" || x.OrderItemType.ToUpper() == "PREORDER"),
            cancellationToken);

        if (!hasPreOrderItems)
        {
            throw new AppException("ORDER_NOT_PREORDER", "Only pre-order orders can be marked as stock ready.", HttpStatusCode.BadRequest);
        }

        if (order.Status != StatusStockRequested && order.Status != StatusPreOrderConfirmed)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Stock can be marked ready only after stock has been requested.", HttpStatusCode.BadRequest);
        }

        order.Status = StatusStockReady;
        order.PreOrderStatus = null;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Hàng đặt trước đã sẵn sàng",
            $"Đơn hàng {order.Id} đã có hàng sẵn sàng để xử lý.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    // Bat dau san xuat don hang.
    public async Task<object> StartProductionAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);
        EnsureNotOnOperationalHold(order);
        var hasPreOrderItems = await _dbContext.OrderItems.AnyAsync(
            x => x.OrderId == orderId
                && x.OrderItemType != null
                && (x.OrderItemType.ToUpper() == "PRE_ORDER" || x.OrderItemType.ToUpper() == "PREORDER"),
            cancellationToken);

        if (hasPreOrderItems)
        {
            if (order.Status != StatusStockReady)
            {
                throw new AppException("INVALID_ORDER_STATUS", "Pre-order can start production only after stock is ready.", HttpStatusCode.BadRequest);
            }
        }
        else if (order.Status != StatusConfirmed)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot start production in current status.", HttpStatusCode.BadRequest);
        }

        order.Status = StatusInProduction;

        var items = await _dbContext.OrderItems.Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);

        // Dong bo trang thai tung order item khi vao giai doan san xuat.
        foreach (var item in items)
        {
            item.Status = GetInitialOrderItemStatus(item.OrderItemType, item.LensId, item.PrescriptionId);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đang sản xuất",
            $"Đơn hàng {order.Id} đã bắt đầu sản xuất.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    // Ket thuc san xuat va chuyen sang san sang giao.
    public async Task<object> FinishProductionAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);
        EnsureNotOnOperationalHold(order);

        if (order.Status != StatusInProduction)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot finish production in current status.", HttpStatusCode.BadRequest);
        }

        order.Status = StatusReadyToShip;

        var items = await _dbContext.OrderItems.Where(x => x.OrderId == orderId).ToListAsync(cancellationToken);

        // Danh dau tat ca item da duoc san xuat xong.
        foreach (var item in items)
        {
            item.Status = "PRODUCED";
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đã hoàn tất sản xuất",
            $"Đơn hàng {order.Id} đã hoàn tất sản xuất.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public async Task<object> BulkReadyToShipAsync(IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken = default)
    {
        var uniqueOrderIds = orderIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (uniqueOrderIds.Count == 0)
        {
            return new { updatedCount = 0 };
        }

        var orders = await _dbContext.Orders
            .Where(x => uniqueOrderIds.Contains(x.Id))
            .ToListAsync(cancellationToken);

        if (orders.Count != uniqueOrderIds.Count)
        {
            throw new AppException("ORDER_NOT_FOUND", "One or more orders were not found.", HttpStatusCode.NotFound);
        }

        foreach (var order in orders)
        {
            EnsureNotOnOperationalHold(order);
            var canMarkReady = order.Status is StatusPending or StatusPaid or StatusConfirmed
                or StatusPreOrderConfirmed or StatusStockRequested or StatusStockReady
                or StatusInProduction or "PROCESSING" or "PREPARING" or "PRODUCED";

            if (!canMarkReady)
            {
                throw new AppException("INVALID_ORDER_STATUS", "Order cannot be marked ready to ship in current status.", HttpStatusCode.BadRequest);
            }

            order.Status = StatusReadyToShip;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        foreach (var order in orders)
        {
            await TryNotifyOrderCustomerAsync(order,
                "Đơn hàng sẵn sàng vận chuyển",
                $"Đơn hàng {order.Id} đã sẵn sàng được giao đến bạn.",
                cancellationToken);
        }

        return new
        {
            updatedCount = orders.Count,
            orderIds = orders.Select(x => x.Id).ToList()
        };
    }

    // Vận hành báo lỗi — tạm dừng đơn ở giai đoạn xử lý kho / sản xuất / giao.
    public async Task<object> ReportOperationalHoldAsync(string orderId, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new AppException("REASON_REQUIRED", "Hold reason is required.", HttpStatusCode.BadRequest);
        }

        var trimmed = reason.Trim();
        if (trimmed.Length > 2000)
        {
            throw new AppException("REASON_TOO_LONG", "Hold reason exceeds maximum length.", HttpStatusCode.BadRequest);
        }

        var order = await GetOrder(orderId, cancellationToken);
        if (order.Status == StatusOnHold)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order is already on hold.", HttpStatusCode.BadRequest);
        }

        if (!CanEnterOperationalHold(order.Status))
        {
            throw new AppException("INVALID_ORDER_STATUS", "Operational hold is not allowed in the current order status.", HttpStatusCode.BadRequest);
        }

        order.StatusBeforeHold = order.Status;
        order.Status = StatusOnHold;
        order.OperationalHoldReason = trimmed;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng tạm giữ",
            $"Đơn hàng của bạn đang được xem xét tạm giữ trong quá trình xử lý. Lý do: {trimmed}",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    // Khôi phục đơn ON_HOLD về trạng thái workflow trước khi tạm giữ.
    public async Task<object> ResumeOperationalHoldAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);
        if (order.Status != StatusOnHold)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order is not on hold.", HttpStatusCode.BadRequest);
        }

        if (string.IsNullOrWhiteSpace(order.StatusBeforeHold))
        {
            throw new AppException("INVALID_HOLD_STATE", "Cannot resume: missing status before hold.", HttpStatusCode.BadRequest);
        }

        order.Status = order.StatusBeforeHold;
        order.StatusBeforeHold = null;
        order.OperationalHoldReason = null;

        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng tiếp tục xử lý",
            $"Đơn hàng {order.Id} đã được tiếp tục trong quy trình xử lý.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
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

        EnsureNotOnOperationalHold(order);

        if (order.Status != StatusInProduction)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order item status can only be updated when order is IN_PRODUCTION.", HttpStatusCode.BadRequest);
        }

        item.Status = status;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(
            order,
            "Cập nhật tiến độ đơn hàng",
            $"Sản phẩm trong đơn hàng {order.Id} đã được cập nhật trạng thái xử lý.",
            cancellationToken);

        return await BuildOrderResponse(item.OrderId, cancellationToken);
    }

    public async Task<object> StartDeliveryAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);

        if (order.Status != StatusReadyToShip)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot start delivery in current status.", HttpStatusCode.BadRequest);
        }

        order.Status = StatusDelivering;
        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đang giao",
            $"Đơn hàng {order.Id} đã được bàn giao cho bộ phận giao hàng.",
            cancellationToken);

        return await BuildOrderResponse(orderId, cancellationToken);
    }

    public async Task<object> ConfirmDeliveredAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(x => x.OrderItems)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        if (order.Status != StatusDelivering)
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot be marked as delivered in current status.", HttpStatusCode.BadRequest);
        }

        order.Status = StatusDelivered;
        order.DeliveredAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await ApplyInventoryOnOrderDeliveredAsync(order, cancellationToken);

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

    // Lay don quan tri theo id.
    public async Task<object> GetManagementOrderByIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        return await BuildOrderResponse(orderId, cancellationToken);
    }

    // Lay danh sach don quan tri theo trang thai bo loc.
    public Task<PagedResultDto<object>> GetManagementOrdersAsync(string? status, int page, int size, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        return GetManagementOrdersInternal(status, onlyPaid: false, null, page, size, sortBy, sortDir, cancellationToken);
    }

    // Lay don quan tri theo khach hang.
    public Task<PagedResultDto<object>> GetManagementOrdersByCustomerAsync(string customerId, int page, int size, string sortBy, string sortDir, CancellationToken cancellationToken = default)
    {
        return GetManagementOrdersInternal(null, onlyPaid: false, customerId, page, size, sortBy, sortDir, cancellationToken);
    }

    // Xoa logic don hang khoi he thong quan tri.
    public async Task DeleteOrderLogicallyAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var order = await GetOrder(orderId, cancellationToken);
        order.Status = "DELETED";
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    // Kiem tra gia ban va muc giam cua mot combo san pham.
    public async Task<object> PriceCheckAsync(PriceCheckDto request, CancellationToken cancellationToken = default)
    {
        var detailRows = new List<object>();
        decimal originalTotal = 0m;

        // Duyet tung item de tinh tong gia goc va chi tiet.
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

    // Lieu ke don hang cho trang quan tri theo bo loc va phan trang.
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
            query = status.Trim().ToUpperInvariant() switch
            {
                StatusPreOrderConfirmed => query.Where(x => x.Status == StatusPreOrderConfirmed),
                StatusConfirmed => query.Where(x => x.Status == StatusConfirmed),
                _ => query.Where(x => x.Status != null && x.Status.ToLower() == status.ToLower())
            };
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
            "totalamount" => desc
                ? query.OrderByDescending(x => x.TotalAmount).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.TotalAmount).ThenBy(x => x.Id),
            _ => desc
                ? query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
                : query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
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

    // Dinh danh order hop le va nap du lieu lien quan.
    private async Task<Order> GetOrder(string orderId, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        return order;
    }

    // Xay dung payload tra ve cho mot don hang.
    private async Task<object> BuildOrderResponse(string orderId, CancellationToken cancellationToken)
    {
#pragma warning disable CS8602
        var order = await _dbContext.Orders
            .Include(x => x.Customer)
            .Include(x => x.Payments)
                .ThenInclude(x => x.Transactions)
            .Include(x => x.OrderItems)
                .ThenInclude(x => x.ProductVariant)
                    .ThenInclude(x => x.Product!)
                        .ThenInclude(x => x.ProductImages)
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
            productImage = ProductImageUrl.ResolveForClient(
                item.ProductVariant?.Product?.ProductImages
                    .Where(x => !string.IsNullOrWhiteSpace(x.ImageUrl))
                    .OrderBy(x => x.Id)
                    .Select(x => x.ImageUrl!)
                    .FirstOrDefault()),
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

        var hasPreOrderItems = order.OrderItems.Any(x => IsPreOrderOrderItemType(x.OrderItemType));
        var responseStatus = GetDisplayStatus(order.Status, hasPreOrderItems, order.PreOrderStatus);

        return new
        {
            customerId = order.CustomerId,
            orderId = order.Id,
            orderName = $"{(hasPreOrderItems ? "PREORDER" : "ORDER")}-{order.Id[..Math.Min(8, order.Id.Length)]}",
            deliveryAddress = order.DeliveryAddress,
            recipientName = order.RecipientName,
            phoneNumber = order.PhoneNumber,
            paymentMethod = order.PaymentMethod,
            orderStatus = responseStatus,
            createdAt = order.CreatedAt,
            cancellationReason = order.CancellationReason,
            cancelledAt = order.CancelledAt,
            cancelledBy = order.CancelledBy,
            totalAmount = order.TotalAmount,
            depositAmount = order.DepositAmount,
            remainingAmount = order.RemainingAmount,
            paidAmount = order.Payments.Where(x => x.Status == "PAID").Sum(x => x.Amount ?? 0m),
            items = itemResults,
            payments,
            bankInfo = new
            {
                bankName = order.BankName,
                bankAccountNumber = order.BankAccountNumber,
                accountHolderName = order.AccountHolderName
            },
            operationalHoldReason = order.OperationalHoldReason,
            statusBeforeHold = order.StatusBeforeHold
        };
    }

    // Xu ly tu choi don hang o nhung trang thai duoc phep.
    private async Task<object> RejectOrderInternalAsync(string orderId, string? reason, string cancelledByRole, CancellationToken cancellationToken)
    {
        var order = await _dbContext.Orders.Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        if (order.Status != StatusOnHold && order.Status is not ("AWAITING_VERIFICATION" or StatusPending or StatusPaid))
        {
            throw new AppException("INVALID_ORDER_STATUS", "Order cannot be rejected in current status.", HttpStatusCode.BadRequest);
        }

        order.StatusBeforeHold = null;
        order.OperationalHoldReason = null;
        order.Status = "CANCELLED";
        ApplyCancellationMetadata(order, reason, cancelledByRole);

        foreach (var item in order.OrderItems)
        {
            if (item.InventoryId is null || !IsInStockLine(item.OrderItemType))
            {
                continue;
            }

            var inventory = await _dbContext.Inventories.FirstOrDefaultAsync(x => x.Id == item.InventoryId, cancellationToken);
            if (inventory is not null)
            {
                inventory.ReservedQuantity = Math.Max(0, (inventory.ReservedQuantity ?? 0) - (item.Quantity ?? 0));

                var variant = await _dbContext.ProductVariants.FirstOrDefaultAsync(x => x.Id == item.ProductVariantId, cancellationToken);
                if (variant is not null)
                {
                    var available = (inventory.Quantity ?? 0) - (inventory.ReservedQuantity ?? 0);
                    variant.OrderItemType = available > 0 ? "IN_STOCK" : "PRE_ORDER";
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        await TryNotifyOrderCustomerAsync(order,
            "Đơn hàng đã bị từ chối",
            string.IsNullOrWhiteSpace(reason)
                ? $"Đơn hàng {order.Id} đã bị từ chối và hủy."
                : $"Đơn hàng {order.Id} đã bị từ chối: {reason}",
            cancellationToken);

        return new
        {
            orderId = order.Id,
            orderStatus = order.Status,
            cancellationReason = order.CancellationReason,
            cancelledAt = order.CancelledAt,
            cancelledBy = order.CancelledBy
        };
    }

    private static void ApplyCancellationMetadata(Order order, string? reason, string cancelledByRole)
    {
        order.CancellationReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        order.CancelledAt = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        order.CancelledBy = cancelledByRole.Trim();
    }

    /// <summary>
    /// Dòng IN_STOCK: tạo đơn đã tăng reserved — giao thành công trừ cả tồn kho và reserved cùng số lượng.
    /// Dòng PRE_ORDER: tạo đơn không giữ reserved — giao thành công chỉ trừ tồn thực (hàng xuất kho).
    /// </summary>
    // Tru ton kho khi don hang da giao thanh cong.
    private async Task ApplyInventoryOnOrderDeliveredAsync(Order order, CancellationToken cancellationToken)
    {
        foreach (var item in order.OrderItems)
        {
            if (item.InventoryId is null)
            {
                continue;
            }

            var q = item.Quantity ?? 0;
            if (q <= 0)
            {
                continue;
            }

            var inventory = await _dbContext.Inventories.FirstOrDefaultAsync(x => x.Id == item.InventoryId, cancellationToken);
            if (inventory is null)
            {
                continue;
            }

            if (IsInStockLine(item.OrderItemType))
            {
                inventory.Quantity = Math.Max(0, (inventory.Quantity ?? 0) - q);
                inventory.ReservedQuantity = Math.Max(0, (inventory.ReservedQuantity ?? 0) - q);
            }
            else
            {
                inventory.Quantity = Math.Max(0, (inventory.Quantity ?? 0) - q);
            }

            var variant = await _dbContext.ProductVariants.FirstOrDefaultAsync(x => x.Id == item.ProductVariantId, cancellationToken);
            if (variant is not null)
            {
                var avail = (inventory.Quantity ?? 0) - (inventory.ReservedQuantity ?? 0);
                variant.OrderItemType = avail > 0 ? "IN_STOCK" : "PRE_ORDER";
            }
        }
    }

    // Kiem tra item co phai dong hang ton kho hay khong.
    private static bool IsInStockLine(string? orderItemType) =>
        string.Equals(orderItemType, "IN_STOCK", StringComparison.OrdinalIgnoreCase);

    // Tao hoac cap nhat prescription cho order item.
    private async Task<string?> UpsertPrescription(string? prescriptionId, PrescriptionDto? request, string? presetImageRelativeUrl, CancellationToken cancellationToken)
    {
        if (request is null && string.IsNullOrWhiteSpace(presetImageRelativeUrl))
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

        if (!string.IsNullOrWhiteSpace(presetImageRelativeUrl))
        {
            prescription.ImageUrl = presetImageRelativeUrl;
        }

        return prescription.Id;
    }

    // Chuyen prescription entity thanh object phan hoi.
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

    // Kiem tra trang thai khach hang co the sua don hay khong.
    private static bool CanCustomerEdit(string? status)
    {
        return status is StatusPending or "PREPARING";
    }

    // Kiem tra trang thai khach hang co the huy don hay khong.
    private static bool CanCustomerCancel(string? status)
    {
        return status is StatusPending or StatusPaid or "PREPARING" or StatusConfirmed or StatusPreOrderConfirmed;
    }

    /// <summary>Đồng bộ với BuildOrderResponse: PRE_ORDER / PREORDER, mọi casing — tránh verify gán CONFIRMED thay vì PREORDER_CONFIRMED.</summary>
    private static bool IsPreOrderOrderItemType(string? orderItemType)
    {
        if (string.IsNullOrWhiteSpace(orderItemType))
        {
            return false;
        }

        var u = orderItemType.Trim().ToUpperInvariant().Replace('-', '_');
        return u is "PRE_ORDER" or "PREORDER";
    }

    // Lay trang thai khoi tao cua tung order item.
    private static string GetInitialOrderItemStatus(string? orderItemType, string? lensId, string? prescriptionId)
    {
        var requiresProduction =
            string.Equals(orderItemType, "PRE_ORDER", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(lensId) ||
            !string.IsNullOrWhiteSpace(prescriptionId);

        return requiresProduction ? "IN_PRODUCTION" : "PRODUCED";
    }

    // Tinh trang thai ban hang dua tren ton kho hien tai.
    private static string ResolveOrderItemType(ProductVariant variant)
    {
        if (variant.Inventory is null)
        {
            return "PRE_ORDER";
        }

        var available = (variant.Inventory.Quantity ?? 0) - (variant.Inventory.ReservedQuantity ?? 0);
        return available > 0 ? "IN_STOCK" : "PRE_ORDER";
    }

    // Gui thong bao ve khach hang sau moi thay doi workflow.
    private async Task TryCreateOrderNotificationAsync(string recipientId, string title, string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(recipientId))
        {
            return;
        }

        var recipientExists = await _dbContext.Users.AnyAsync(x => x.Id == recipientId, cancellationToken);
        if (!recipientExists)
        {
            _logger.LogWarning(
                "Order notification skipped: recipient {RecipientId} not found in Users.",
                recipientId);
            return;
        }

        try
        {
            await _notificationService.CreateAsync("SYSTEM", new CreateNotificationDto
            {
                RecipientId = recipientId,
                Title = title,
                Content = content
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Order notification failed for recipient {RecipientId}.",
                recipientId);
        }
    }

    // Bao dam order co customerId hop le truoc khi gui thong bao.
    private async Task TryNotifyOrderCustomerAsync(Order order, string title, string content, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(order.CustomerId))
        {
            return;
        }

        await TryCreateOrderNotificationAsync(order.CustomerId, title, content, cancellationToken);
    }

    // Tra ve trang thai hien thi cho API, neu chua co thi tra string rong.
    private static string GetDisplayStatus(string? status, bool hasPreOrderItems, string? preOrderStatus = null)
    {
        return status ?? string.Empty;
    }

    private static void EnsureNotOnOperationalHold(Order order)
    {
        if (order.Status == StatusOnHold)
        {
            throw new AppException(
                "ORDER_ON_HOLD",
                "Order is on hold. Resume processing before continuing.",
                HttpStatusCode.BadRequest);
        }
    }

    private static bool CanEnterOperationalHold(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return false;
        }

        return status == StatusConfirmed
            || status == StatusPreOrderConfirmed
            || status == StatusStockRequested
            || status == StatusStockReady
            || status == StatusInProduction
            || status == "PROCESSING"
            || status == "PREPARING"
            || status == "PRODUCED";
    }
}
