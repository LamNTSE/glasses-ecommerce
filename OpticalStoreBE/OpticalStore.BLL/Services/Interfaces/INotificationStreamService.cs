using System.Threading.Channels;
using OpticalStore.BLL.DTOs.Notifications;

namespace OpticalStore.BLL.Services.Interfaces;

public sealed record NotificationStreamSubscription(Guid SubscriptionId, ChannelReader<NotificationResponseDto> Reader);

public interface INotificationStreamService
{
    NotificationStreamSubscription Subscribe(string userId);

    Task PublishAsync(string userId, NotificationResponseDto notification, CancellationToken cancellationToken = default);

    void Unsubscribe(string userId, Guid subscriptionId);
}
