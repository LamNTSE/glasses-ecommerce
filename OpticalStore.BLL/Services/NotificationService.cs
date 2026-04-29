using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Notifications;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class NotificationService : INotificationService
{
    private readonly OpticalStoreDbContext _dbContext;
    private readonly INotificationStreamService _notificationStreamService;

    // Khoi tao service thong bao voi db context va stream service.
    public NotificationService(OpticalStoreDbContext dbContext, INotificationStreamService notificationStreamService)
    {
        _dbContext = dbContext;
        _notificationStreamService = notificationStreamService;
    }

    // Tao thong bao moi va day sang realtime stream.
    public async Task<NotificationResponseDto> CreateAsync(string senderId, CreateNotificationDto request, CancellationToken cancellationToken = default)
    {
        var recipient = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == request.RecipientId, cancellationToken);
        if (recipient is null)
        {
            throw new AppException("USER_NOT_EXISTED", "Recipient user not found.", HttpStatusCode.NotFound);
        }

        var notification = new Notification
        {
            Id = Guid.NewGuid().ToString(),
            RecipientId = recipient.Id,
            SenderId = string.IsNullOrWhiteSpace(senderId) ? "SYSTEM" : senderId,
            Title = request.Title.Trim(),
            Content = request.Content.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync(cancellationToken);

        notification.Recipient = recipient;
        var result = Map(notification);
        await _notificationStreamService.PublishAsync(notification.RecipientId, result, cancellationToken);

        return result;
    }

    // Lay tat ca thong bao cua mot user theo thu tu moi nhat.
    public async Task<List<NotificationResponseDto>> GetMyNotificationsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var data = await _dbContext.Notifications
            .Include(x => x.Recipient)
            .Where(x => x.RecipientId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return data.Select(Map).ToList();
    }

    // Dem so thong bao chua doc cua user.
    public Task<long> GetMyUnreadCountAsync(string userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Notifications.LongCountAsync(x => x.RecipientId == userId && !x.IsRead, cancellationToken);
    }

    // Danh dau mot thong bao la da doc va day cap nhat sang stream.
    public async Task<NotificationResponseDto> MarkAsReadAsync(string userId, string notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .Include(x => x.Recipient)
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.RecipientId == userId, cancellationToken);

        if (notification is null)
        {
            throw new AppException("NOTIFICATION_NOT_FOUND", "Notification not found.", HttpStatusCode.NotFound);
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var result = Map(notification);
        await _notificationStreamService.PublishAsync(userId, result, cancellationToken);
        return result;
    }

    // Danh dau toan bo thong bao chua doc la da doc.
    public async Task<int> MarkAllAsReadAsync(string userId, CancellationToken cancellationToken = default)
    {
        var notifications = await _dbContext.Notifications
            .Include(x => x.Recipient)
            .Where(x => x.RecipientId == userId && !x.IsRead)
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;

        // Cap nhat trang thai doc cho tung thong bao truoc khi luu.
        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Day tung thong bao da cap nhat ra stream de dong bo UI.
        foreach (var notification in notifications)
        {
            await _notificationStreamService.PublishAsync(userId, Map(notification), cancellationToken);
        }

        return notifications.Count;
    }

    private static NotificationResponseDto Map(Notification notification)
    {
        return new NotificationResponseDto
        {
            Id = notification.Id,
            RecipientId = notification.RecipientId,
            RecipientName = ResolveRecipientName(notification.Recipient),
            Title = notification.Title,
            Content = notification.Content,
            SenderId = notification.SenderId,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
        };
    }

    // Lay ten hien thi cua nguoi nhan theo thu tu uu tien ho ten, username, email.
    private static string? ResolveRecipientName(User? recipient)
    {
        if (recipient is null)
        {
            return null;
        }

        var fullName = string.Join(" ", new[] { recipient.FirstName, recipient.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Select(part => part!.Trim()));

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        if (!string.IsNullOrWhiteSpace(recipient.Username))
        {
            return recipient.Username;
        }

        return recipient.Email;
    }
}
