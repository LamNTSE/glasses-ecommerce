using OpticalStore.BLL.DTOs.Payments;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IPaymentWorkflowService
{
    Task<PaymentRequirementResultDto> GetPaymentRequirementAsync(PaymentRequirementDto request, CancellationToken cancellationToken = default);

    Task<string> CheckoutAsync(string orderId, CancellationToken cancellationToken = default);

    Task<string> HandleVnPayCallbackAsync(string? paymentId, string? transactionStatus, CancellationToken cancellationToken = default);

    Task<List<PaymentHistoryItemDto>> GetPaymentHistoryAsync(string orderId, CancellationToken cancellationToken = default);
}
