using OpticalStore.BLL.DTOs.Payments;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IPaymentWorkflowService
{
    Task<PaymentRequirementResultDto> GetPaymentRequirementAsync(PaymentRequirementDto request, CancellationToken cancellationToken = default);

    Task<string> CheckoutAsync(string orderId, string? clientIpAddress = null, CancellationToken cancellationToken = default);

    Task<VnPayProcessResultDto> HandleVnPayReturnAsync(IReadOnlyDictionary<string, string> query, CancellationToken cancellationToken = default);

    Task<VnPayProcessResultDto> HandleVnPayIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken cancellationToken = default);

    Task<List<PaymentHistoryItemDto>> GetPaymentHistoryAsync(string orderId, CancellationToken cancellationToken = default);
}
