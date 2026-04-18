using OpticalStore.BLL.DTOs.Refunds;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IRefundWorkflowService
{
    Task<object> InactivateVariantAsync(string variantId, CancellationToken cancellationToken = default);

    Task<List<object>> GetAffectedOrdersAsync(string variantId, CancellationToken cancellationToken = default);

    Task<List<object>> CreateBatchAsync(RefundBatchDto request, CancellationToken cancellationToken = default);

    Task<List<object>> GetReadyAsync(CancellationToken cancellationToken = default);

    Task<string> RefundCheckoutAsync(string refundId, CancellationToken cancellationToken = default);
}
