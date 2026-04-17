using System.Collections.Concurrent;
using System.Threading.Channels;
using OpticalStore.BLL.DTOs.Notifications;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.BLL.Services;

public sealed class NotificationStreamService : INotificationStreamService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<NotificationResponseDto>>> _subscriptions = new();

    public NotificationStreamSubscription Subscribe(string userId)
    {
        var channel = Channel.CreateUnbounded<NotificationResponseDto>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        var subscriptionId = Guid.NewGuid();
        var userSubscriptions = _subscriptions.GetOrAdd(userId, _ => new ConcurrentDictionary<Guid, Channel<NotificationResponseDto>>());
        userSubscriptions[subscriptionId] = channel;

        return new NotificationStreamSubscription(subscriptionId, channel.Reader);
    }

    public Task PublishAsync(string userId, NotificationResponseDto notification, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(userId, out var userSubscriptions))
        {
            return Task.CompletedTask;
        }

        foreach (var (subscriptionId, channel) in userSubscriptions)
        {
            if (!channel.Writer.TryWrite(notification))
            {
                userSubscriptions.TryRemove(subscriptionId, out _);
            }
        }

        return Task.CompletedTask;
    }

    public void Unsubscribe(string userId, Guid subscriptionId)
    {
        if (!_subscriptions.TryGetValue(userId, out var userSubscriptions))
        {
            return;
        }

        if (!userSubscriptions.TryRemove(subscriptionId, out var channel))
        {
            return;
        }

        channel.Writer.TryComplete();

        if (userSubscriptions.IsEmpty)
        {
            _subscriptions.TryRemove(userId, out _);
        }
    }
}
