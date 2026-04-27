using OpticalStore.BLL.DTOs.Feedbacks;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IFeedbackWorkflowService
{
    Task<FeedbackResponseDto> CreateAsync(FeedbackCreateDto request, string userId, bool isAdmin, List<string>? uploadedImageNames, CancellationToken cancellationToken = default);

    Task<FeedbackResponseDto> UpdateAsync(string feedbackId, FeedbackUpdateDto request, string userId, bool isAdmin, List<string>? uploadedImageNames, CancellationToken cancellationToken = default);

    Task DeleteAsync(string feedbackId, string userId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<List<FeedbackResponseDto>> GetByProductAsync(string productId, CancellationToken cancellationToken = default);

    Task<List<FeedbackResponseDto>> GetMineAsync(string userId, CancellationToken cancellationToken = default);

    Task<List<FeedbackResponseDto>> GetByOrderAsync(string orderId, string userId, bool isAdmin, CancellationToken cancellationToken = default);

    Task<FeedbackResponseDto> GetByIdAsync(string feedbackId, CancellationToken cancellationToken = default);
}
