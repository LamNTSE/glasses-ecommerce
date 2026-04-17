using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Requests.Notifications;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Notifications;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("notifications")]
[Tags("14. Notifications")]
public sealed class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly INotificationStreamService _notificationStreamService;

    public NotificationController(INotificationService notificationService, INotificationStreamService notificationStreamService)
    {
        _notificationService = notificationService;
        _notificationStreamService = notificationStreamService;
    }

    [HttpGet("stream")]
    [Authorize]
    public async Task Stream(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("userId") ?? string.Empty;
        var subscription = _notificationStreamService.Subscribe(userId);

        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        try
        {
            await WriteEventAsync("connected", new { connected = true }, cancellationToken);

            await foreach (var notification in subscription.Reader.ReadAllAsync(HttpContext.RequestAborted))
            {
                await WriteEventAsync("notification", notification, cancellationToken);
            }
        }
        finally
        {
            _notificationStreamService.Unsubscribe(userId, subscription.SubscriptionId);
        }
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<ActionResult<ApiResponse<NotificationResponseDto>>> Create([FromBody] CreateNotificationRequest request, CancellationToken cancellationToken)
    {
        var senderId = User.FindFirstValue("userId") ?? "SYSTEM";
        var result = await _notificationService.CreateAsync(senderId, new CreateNotificationDto
        {
            RecipientId = request.RecipientId,
            Title = request.Title,
            Content = request.Content
        }, cancellationToken);

        return Ok(new ApiResponse<NotificationResponseDto> { Result = result });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<NotificationResponseDto>>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("userId") ?? string.Empty;
        var result = await _notificationService.GetMyNotificationsAsync(userId, cancellationToken);
        return Ok(new ApiResponse<List<NotificationResponseDto>> { Result = result });
    }

    [HttpGet("me/unread-count")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("userId") ?? string.Empty;
        var unreadCount = await _notificationService.GetMyUnreadCountAsync(userId, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Result = new { unreadCount }
        });
    }

    [HttpPatch("{notificationId}/read")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<NotificationResponseDto>>> MarkAsRead(string notificationId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("userId") ?? string.Empty;
        var result = await _notificationService.MarkAsReadAsync(userId, notificationId, cancellationToken);
        return Ok(new ApiResponse<NotificationResponseDto> { Result = result });
    }

    [HttpPatch("me/read-all")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("userId") ?? string.Empty;
        var updated = await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Result = new { updated }
        });
    }

    private async Task WriteEventAsync(string eventName, object data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data);
        await Response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}


