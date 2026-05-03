using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Mappings;
using OpticalStore.API.Requests.Orders;
using OpticalStore.API.Responses;
using OpticalStore.API.Swagger;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Tags("09. Orders")]
public sealed class OrdersWorkflowController : ControllerBase
{
    private readonly IOrdersWorkflowService _ordersWorkflowService;
    private readonly IWebHostEnvironment _environment;

    // Khoi tao controller va gan service xu ly don hang.
    public OrdersWorkflowController(IOrdersWorkflowService ordersWorkflowService, IWebHostEnvironment environment)
    {
        _ordersWorkflowService = ordersWorkflowService;
        _environment = environment;
    }

    // Tao don hang moi, kem upload anh don neu co.
    [HttpPost("orders/create")]
    [Authorize(Roles = "CUSTOMER,ADMIN,MANAGER")]
    [Consumes("multipart/form-data")]
    [SwaggerMultipartJsonPart("orderInfo", typeof(CreateOrderRequest))]
    public async Task<ActionResult<ApiResponse<object>>> CreateOrder(
        [FromForm] string orderInfo,
        [FromQuery(Name = "PaymentMethod")] string paymentMethod,
        IFormFile? prescriptionImage,
        CancellationToken cancellationToken)
    {
        var request = ParseJsonPayload<CreateOrderRequest>(orderInfo, "orderInfo");
        var userId = GetCurrentUserId();
        string? imageRelativePath = null;

        // Luu anh don y khoa len storage neu client gui kem file.
        if (prescriptionImage is { Length: > 0 })
        {
            imageRelativePath = await PrescriptionImageStorage.SaveAsync(prescriptionImage, _environment, cancellationToken);
        }

        var result = await _ordersWorkflowService.CreateOrderAsync(request.ToDto(), paymentMethod, userId, imageRelativePath, cancellationToken);

        return Ok(new ApiResponse<object> { Result = result });
    }

    // Lay danh sach don hang cua nguoi dung hien tai.
    [HttpGet("orders/me")]
    [Authorize(Roles = "CUSTOMER,ADMIN,MANAGER")]
    public async Task<ActionResult<ApiResponse<object>>> GetMyOrders(
        [FromQuery] string? status,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _ordersWorkflowService.GetMyOrdersAsync(userId, status, page, size, sortBy, sortDir, cancellationToken);

        return Ok(new ApiResponse<object> { Result = result });
    }

    // Lay chi tiet don hang theo id cho chu don hoac admin.
    [HttpGet("orders/{orderId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN,MANAGER")]
    public async Task<ActionResult<ApiResponse<object>>> GetOrderById(string orderId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _ordersWorkflowService.GetOrderByIdAsync(orderId, userId, User.IsInRole("ADMIN"), cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Lay cac don da huy cua chinh nguoi dung hien tai.
    [HttpGet("orders/me/cancelled")]
    [Authorize(Roles = "CUSTOMER,ADMIN,MANAGER")]
    public Task<ActionResult<ApiResponse<object>>> GetMyCancelled(
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        return GetMyOrders("CANCELLED", page, size, sortBy, sortDir, cancellationToken);
    }

    // Cap nhat thong tin don hang khi con cho phep sua.
    [HttpPut("orders/{orderId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN,MANAGER")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateOrder(string orderId, [FromBody] UpdateOrderRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _ordersWorkflowService.UpdateOrderAsync(orderId, request.ToDto(), userId, User.IsInRole("ADMIN"), cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Huy don hang.
    [HttpPut("orders/{orderId}/cancel")]
    [Authorize(Roles = "CUSTOMER,ADMIN,MANAGER,SALE")]
    public async Task<ActionResult<ApiResponse<object>>> CancelOrder(string orderId, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.CancelOrderAsync(orderId, reason, GetCurrentUserRole(), cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Xac nhan hoan tat don hang.
    [HttpPut("orders/{orderId}/complete")]
    [Authorize(Roles = "CUSTOMER,ADMIN,MANAGER,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> CompleteOrder(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.CompleteOrderAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Tai anh don y khoa len storage va gan cho order item.
    [HttpPut("orders/items/{orderItemId}/prescription-image")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<object>>> UploadPrescriptionImage(
        string orderItemId,
        IFormFile? prescriptionImage,
        CancellationToken cancellationToken)
    {
        if (prescriptionImage is not { Length: > 0 })
        {
            throw new AppException("INVALID_FILE", "Prescription image file is required.", HttpStatusCode.BadRequest);
        }

        // Luu file truoc khi gui duong dan sang service nghiep vu.
        var imageRelativePath = await PrescriptionImageStorage.SaveAsync(prescriptionImage, _environment, cancellationToken);
        var result = await _ordersWorkflowService.UploadPrescriptionImageAsync(orderItemId, imageRelativePath, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("orders/items/{orderItemId}/prescription")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> UpdatePrescription(string orderItemId, [FromBody] PrescriptionRequest request, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.UpdatePrescriptionAsync(orderItemId, request.ToDto(), cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Duyet don hang trong luu luong ban hang.
    [HttpPut("sales/orders/{orderId}/verify")]
    [Authorize(Roles = "SALE,ADMIN,MANAGER")]
    public async Task<ActionResult<ApiResponse<object>>> VerifyOrder(string orderId, [FromQuery] bool isApproved, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.VerifyOrderAsync(orderId, isApproved, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Quay lui thao tac verify khi workflow khong ho tro.
    [HttpPut("sales/orders/{orderId}/revert-verify")]
    [Authorize(Roles = "SALE,ADMIN,MANAGER")]
    public async Task<ActionResult<ApiResponse<object>>> RevertVerifyOrder(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.RevertVerifyOrderAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Tu choi don hang theo ly do neu co.
    [HttpPut("sales/orders/{orderId}/reject")]
    [Authorize(Roles = "SALE,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> RejectOrder(string orderId, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.RejectOrderAsync(orderId, reason, GetCurrentUserRole(), cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Yeu cau nhap kho cho don preorder.
    [HttpPut("operation/orders/{orderId}/request-stock")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> RequestStock(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.RequestStockAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Danh dau hang da san sang de chuyen buoc tiep theo.
    [HttpPut("management/orders/{orderId}/stock-ready")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> MarkStockReady(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.MarkStockReadyAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Bat dau san xuat don hang.
    [HttpPut("production/orders/{orderId}/start")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> StartProduction(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.StartProductionAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Ket thuc san xuat va chuyen sang trang thai san sang giao.
    [HttpPut("production/orders/{orderId}/finish")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> FinishProduction(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.FinishProductionAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("production/orders/{orderId}/report-hold")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> ReportOperationalHold(
        string orderId,
        [FromQuery] string? reason,
        CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.ReportOperationalHoldAsync(orderId, reason ?? string.Empty, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("management/orders/{orderId}/resume-from-hold")]
    [Authorize(Roles = "OPERATION,MANAGER,ADMIN,SALE")]
    public async Task<ActionResult<ApiResponse<object>>> ResumeFromOperationalHold(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.ResumeOperationalHoldAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Cap nhat nhieu don sang san sang giao cung luc.
    [HttpPut("production/orders/ready-to-ship")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> BulkReadyToShip([FromBody] BulkOrderIdsRequest request, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.BulkReadyToShipAsync(request.OrderIds, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Cap nhat trang thai tung order item.
    [HttpPut("production/orders/items/{orderItemId}/status")]
    [Authorize(Roles = "OPERATION,ADMIN,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateItemStatus(string orderItemId, [FromQuery] string status, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.UpdateItemStatusAsync(orderItemId, status, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Chuyen don sang buoc giao hang.
    [HttpPatch("management/orders/{orderId}/start-delivery")]
    [Authorize(Roles = "OPERATION,ADMIN,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> StartDelivery(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.StartDeliveryAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Xac nhan don da giao thanh cong.
    [HttpPatch("management/orders/{orderId}/confirm-delivered")]
    [Authorize(Roles = "OPERATION,ADMIN,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmDelivered(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.ConfirmDeliveredAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Lay danh sach don huy da thanh toan trong trang quan tri.
    [HttpGet("management/orders/cancelled/paid")]
    [Authorize(Roles = "MANAGER,ADMIN,SALE,OPERATION")]
    public async Task<ActionResult<ApiResponse<object>>> GetCancelledPaidOrders(
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var result = await _ordersWorkflowService.GetCancelledPaidOrdersAsync(page, size, sortBy, sortDir, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Lay don quan tri theo id.
    [HttpGet("management/orders/{orderId}")]
    [Authorize(Roles = "MANAGER,ADMIN,SALE,OPERATION,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> GetManagementOrderById(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.GetManagementOrderByIdAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Lay danh sach don quan tri theo trang thai bo loc.
    [HttpGet("management/orders")]
    [Authorize(Roles = "MANAGER,ADMIN,SALE,OPERATION,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> GetManagementOrders(
        [FromQuery] string? status,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var result = await _ordersWorkflowService.GetManagementOrdersAsync(status, page, size, sortBy, sortDir, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Lay don quan tri theo khach hang.
    [HttpGet("management/orders/customer/{customerId}")]
    [Authorize(Roles = "MANAGER,ADMIN,SALE,OPERATION")]
    public async Task<ActionResult<ApiResponse<object>>> GetManagementOrdersByCustomer(
        string customerId,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var result = await _ordersWorkflowService.GetManagementOrdersByCustomerAsync(customerId, page, size, sortBy, sortDir, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    // Xoa logic don hang khoi he thong quan tri.
    [HttpDelete("management/orders/{orderId}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteOrderLogically(string orderId, CancellationToken cancellationToken)
    {
        await _ordersWorkflowService.DeleteOrderLogicallyAsync(orderId, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Message = "Order deleted successfully from system logs",
            Result = null
        });
    }

    // Kiem tra gia ban va giam gia cua danh sach san pham.
    [HttpPost("api/orders/price-check")]
    [Authorize(Roles = "SALE,ADMIN,OPERATION")]
    public async Task<ActionResult<ApiResponse<object>>> PriceCheck([FromBody] PriceCheckRequest request, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.PriceCheckAsync(request.ToDto(), cancellationToken);
        return Ok(new ApiResponse<object>
        {
            Message = "Kiểm tra giá thành công",
            Result = result
        });
    }

    // Phan tich payload JSON nhan tu form de tranh loi deserialize am thuc.
    private static T ParseJsonPayload<T>(string payload, string fieldName)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<T>(
                payload,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (parsed is null)
            {
                throw new AppException("INVALID_PAYLOAD", $"Invalid {fieldName} payload.", HttpStatusCode.BadRequest);
            }

            return parsed;
        }
        catch (JsonException ex)
        {
            throw new AppException(
                "INVALID_PAYLOAD",
                $"{fieldName} JSON không hợp lệ: {ex.Message}",
                HttpStatusCode.BadRequest);
        }
    }

    // Lay userId tu claim cua request hien tai.
    private string GetCurrentUserId()
    {
        return User.FindFirstValue("userId")
            ?? throw new AppException("UNAUTHENTICATED", "Missing userId claim.", HttpStatusCode.Unauthorized);
    }

    // Lay role hien tai de luu audit nguoi/nhom huy don.
    private string GetCurrentUserRole()
    {
        var role = new[] { "ADMIN", "MANAGER", "SALE", "CUSTOMER", "SHIPPER" }
            .FirstOrDefault(User.IsInRole)
            ?? User.FindFirstValue(ClaimTypes.Role);

        return role
            ?? throw new AppException("UNAUTHENTICATED", "Missing role claim.", HttpStatusCode.Unauthorized);
    }
}
