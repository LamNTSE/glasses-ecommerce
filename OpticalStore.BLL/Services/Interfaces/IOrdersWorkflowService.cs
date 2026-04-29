using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Orders;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IOrdersWorkflowService
{
    Task<object> CreateOrderAsync(CreateOrderDto request, string? paymentMethod, string userId, string? prescriptionImageRelativeUrl, CancellationToken cancellationToken = default);

    Task<PagedResultDto<object>> GetMyOrdersAsync(string userId, string? status, int page, int size, string sortBy, string sortDir, CancellationToken cancellationToken = default);

    Task<object> GetOrderByIdAsync(string orderId, string userId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<object> UpdateOrderAsync(string orderId, UpdateOrderDto request, string userId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<object> CancelOrderAsync(string orderId, string? cancellationReason, string cancelledByRole, CancellationToken cancellationToken = default);

    Task<object> CompleteOrderAsync(string orderId, CancellationToken cancellationToken = default);

    Task<object> UploadPrescriptionImageAsync(string orderItemId, string prescriptionImageRelativeUrl, CancellationToken cancellationToken = default);

    Task<object> UpdatePrescriptionAsync(string orderItemId, PrescriptionDto request, CancellationToken cancellationToken = default);

    Task<object> VerifyOrderAsync(string orderId, bool isApproved, CancellationToken cancellationToken = default);

    Task<object> RevertVerifyOrderAsync(string orderId, CancellationToken cancellationToken = default);

    Task<object> RejectOrderAsync(string orderId, string? reason, string cancelledByRole, CancellationToken cancellationToken = default);

    Task<object> RequestStockAsync(string orderId, CancellationToken cancellationToken = default);

    Task<object> MarkStockReadyAsync(string orderId, CancellationToken cancellationToken = default);

    Task<object> StartProductionAsync(string orderId, CancellationToken cancellationToken = default);

    Task<object> FinishProductionAsync(string orderId, CancellationToken cancellationToken = default);

    Task<object> BulkReadyToShipAsync(IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken = default);

    Task<object> UpdateItemStatusAsync(string orderItemId, string status, CancellationToken cancellationToken = default);

    Task<object> StartDeliveryAsync(string orderId, CancellationToken cancellationToken = default);

    Task<object> ConfirmDeliveredAsync(string orderId, CancellationToken cancellationToken = default);

    Task<PagedResultDto<object>> GetCancelledPaidOrdersAsync(int page, int size, string sortBy, string sortDir, CancellationToken cancellationToken = default);

    Task<object> GetManagementOrderByIdAsync(string orderId, CancellationToken cancellationToken = default);

    Task<PagedResultDto<object>> GetManagementOrdersAsync(string? status, int page, int size, string sortBy, string sortDir, CancellationToken cancellationToken = default);

    Task<PagedResultDto<object>> GetManagementOrdersByCustomerAsync(string customerId, int page, int size, string sortBy, string sortDir, CancellationToken cancellationToken = default);

    Task DeleteOrderLogicallyAsync(string orderId, CancellationToken cancellationToken = default);

    Task<object> PriceCheckAsync(PriceCheckDto request, CancellationToken cancellationToken = default);
}
