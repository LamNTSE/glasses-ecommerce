using OpticalStore.BLL.DTOs.Notifications;

namespace OpticalStore.BLL.Services.Interfaces;

public interface INotificationService
{
    Task<NotificationResponseDto> CreateAsync(string senderId, CreateNotificationDto request, CancellationToken cancellationToken = default);

    Task<List<NotificationResponseDto>> GetMyNotificationsAsync(string userId, CancellationToken cancellationToken = default);

    Task<long> GetMyUnreadCountAsync(string userId, CancellationToken cancellationToken = default);

    Task<NotificationResponseDto> MarkAsReadAsync(string userId, string notificationId, CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default);
}
