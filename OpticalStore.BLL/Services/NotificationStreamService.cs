using System.Collections.Concurrent;
using System.Threading.Channels;
using OpticalStore.BLL.DTOs.Notifications;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.BLL.Services;

public sealed class NotificationStreamService : INotificationStreamService
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Channel<NotificationResponseDto>>> _subscriptions = new();

    // Tao kenh stream moi cho user va tra ve subscription handle.
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

    // Phat thong bao toi tat ca subscription cua user hien tai.
    public Task PublishAsync(string userId, NotificationResponseDto notification, CancellationToken cancellationToken = default)
    {
        if (!_subscriptions.TryGetValue(userId, out var userSubscriptions))
        {
            return Task.CompletedTask;
        }

        // Neu mot channel khong con nhan duoc du lieu thi go bo subscription do.
        foreach (var (subscriptionId, channel) in userSubscriptions)
        {
            if (!channel.Writer.TryWrite(notification))
            {
                userSubscriptions.TryRemove(subscriptionId, out _);
            }
        }

        return Task.CompletedTask;
    }

    // Huy subscription va giai phong kenh stream.
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
