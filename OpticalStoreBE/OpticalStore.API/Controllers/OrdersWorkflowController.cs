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

    public OrdersWorkflowController(IOrdersWorkflowService ordersWorkflowService, IWebHostEnvironment environment)
    {
        _ordersWorkflowService = ordersWorkflowService;
        _environment = environment;
    }

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
        if (prescriptionImage is { Length: > 0 })
        {
            imageRelativePath = await PrescriptionImageStorage.SaveAsync(prescriptionImage, _environment, cancellationToken);
        }

        var result = await _ordersWorkflowService.CreateOrderAsync(request.ToDto(), paymentMethod, userId, imageRelativePath, cancellationToken);

        return Ok(new ApiResponse<object> { Result = result });
    }

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

    [HttpGet("orders/{orderId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN,MANAGER")]
    public async Task<ActionResult<ApiResponse<object>>> GetOrderById(string orderId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _ordersWorkflowService.GetOrderByIdAsync(orderId, userId, User.IsInRole("ADMIN"), cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

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

    [HttpPut("orders/{orderId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN,MANAGER")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateOrder(string orderId, [FromBody] UpdateOrderRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _ordersWorkflowService.UpdateOrderAsync(orderId, request.ToDto(), userId, User.IsInRole("ADMIN"), cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("orders/{orderId}/cancel")]
    [Authorize(Roles = "CUSTOMER,ADMIN,MANAGER,SALE")]
    public async Task<ActionResult<ApiResponse<object>>> CancelOrder(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.CancelOrderAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("orders/{orderId}/complete")]
    [Authorize(Roles = "CUSTOMER,ADMIN,MANAGER,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> CompleteOrder(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.CompleteOrderAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("orders/items/{orderItemId}/prescription-image")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> UploadPrescriptionImage(string orderItemId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is not { Length: > 0 })
        {
            throw new AppException("INVALID_FILE", "Prescription image file is required.", HttpStatusCode.BadRequest);
        }

        var imageRelativePath = await PrescriptionImageStorage.SaveAsync(file, _environment, cancellationToken);
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

    [HttpPut("sales/orders/{orderId}/verify")]
    [Authorize(Roles = "SALE,ADMIN,MANAGER")]
    public async Task<ActionResult<ApiResponse<object>>> VerifyOrder(string orderId, [FromQuery] bool isApproved, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.VerifyOrderAsync(orderId, isApproved, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("sales/orders/{orderId}/revert-verify")]
    [Authorize(Roles = "SALE,ADMIN,MANAGER")]
    public async Task<ActionResult<ApiResponse<object>>> RevertVerifyOrder(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.RevertVerifyOrderAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("sales/orders/{orderId}/reject")]
    [Authorize(Roles = "SALE,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> RejectOrder(string orderId, [FromQuery] string? reason, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.RejectOrderAsync(orderId, reason, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("operation/orders/{orderId}/request-stock")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> RequestStock(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.RequestStockAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("management/orders/{orderId}/stock-ready")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> MarkStockReady(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.MarkStockReadyAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("production/orders/{orderId}/start")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> StartProduction(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.StartProductionAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("production/orders/{orderId}/finish")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> FinishProduction(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.FinishProductionAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("production/orders/ready-to-ship")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> BulkReadyToShip([FromBody] BulkOrderIdsRequest request, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.BulkReadyToShipAsync(request.OrderIds, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPut("production/orders/items/{orderItemId}/status")]
    [Authorize(Roles = "OPERATION,ADMIN,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateItemStatus(string orderItemId, [FromQuery] string status, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.UpdateItemStatusAsync(orderItemId, status, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPatch("management/orders/{orderId}/start-delivery")]
    [Authorize(Roles = "OPERATION,ADMIN,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> StartDelivery(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.StartDeliveryAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPatch("management/orders/{orderId}/confirm-delivered")]
    [Authorize(Roles = "OPERATION,ADMIN,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> ConfirmDelivered(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.ConfirmDeliveredAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

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

    [HttpGet("management/orders/{orderId}")]
    [Authorize(Roles = "MANAGER,ADMIN,SALE,OPERATION,SHIPPER")]
    public async Task<ActionResult<ApiResponse<object>>> GetManagementOrderById(string orderId, CancellationToken cancellationToken)
    {
        var result = await _ordersWorkflowService.GetManagementOrderByIdAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

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

    private static T ParseJsonPayload<T>(string payload, string fieldName)
    {
        var parsed = JsonSerializer.Deserialize<T>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (parsed is null)
        {
            throw new AppException("INVALID_PAYLOAD", $"Invalid {fieldName} payload.", HttpStatusCode.BadRequest);
        }

        return parsed;
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue("userId")
            ?? throw new AppException("UNAUTHENTICATED", "Missing userId claim.", HttpStatusCode.Unauthorized);
    }
}
