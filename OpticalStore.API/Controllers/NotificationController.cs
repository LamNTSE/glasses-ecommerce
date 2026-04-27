using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Mappings;
using OpticalStore.API.Requests.Notifications;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Notifications;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("notifications")]
[Tags("13. Notifications")]
public sealed class NotificationController : ControllerBase
{
    private static readonly JsonSerializerOptions SseJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(15);

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
        var userId = GetCurrentUserId();

        var subscription = _notificationStreamService.Subscribe(userId);
        var heartbeatTimer = new PeriodicTimer(HeartbeatInterval);

        Response.Headers.Append("Content-Type", "text/event-stream; charset=utf-8");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            await WriteRawEventAsync("connected", "Notification SSE connected", cancellationToken);

            var waitForNotificationTask = subscription.Reader.WaitToReadAsync(cancellationToken).AsTask();

            while (!cancellationToken.IsCancellationRequested)
            {
                var heartbeatTask = heartbeatTimer.WaitForNextTickAsync(cancellationToken).AsTask();

                var completedTask = await Task.WhenAny(waitForNotificationTask, heartbeatTask);
                if (completedTask == heartbeatTask)
                {
                    await WriteHeartbeatAsync(cancellationToken);
                    continue;
                }

                if (!await waitForNotificationTask)
                {
                    break;
                }

                while (subscription.Reader.TryRead(out var notification))
                {
                    await WriteEventAsync("notification", notification, cancellationToken);
                }

                waitForNotificationTask = subscription.Reader.WaitToReadAsync(cancellationToken).AsTask();
            }
        }
        catch (OperationCanceledException) when (HttpContext.RequestAborted.IsCancellationRequested)
        {
            // Client closed the SSE connection. This is expected and should not be logged as an error.
        }
        finally
        {
            heartbeatTimer.Dispose();
            _notificationStreamService.Unsubscribe(userId, subscription.SubscriptionId);
        }
    }

    [HttpPost]
    [Authorize(Roles = "ADMIN,MANAGER")]
    public async Task<ActionResult<ApiResponse<NotificationResponseDto>>> Create([FromBody] CreateNotificationRequest request, CancellationToken cancellationToken)
    {
        var senderId = GetCurrentUserId();
        var result = await _notificationService.CreateAsync(senderId, request.ToDto(), cancellationToken);

        return Ok(new ApiResponse<NotificationResponseDto> { Result = result });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<NotificationResponseDto>>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.GetMyNotificationsAsync(userId, cancellationToken);
        return Ok(new ApiResponse<List<NotificationResponseDto>> { Result = result });
    }

    [HttpGet("me/unread-count")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
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
        var userId = GetCurrentUserId();
        var result = await _notificationService.MarkAsReadAsync(userId, notificationId, cancellationToken);
        return Ok(new ApiResponse<NotificationResponseDto> { Result = result });
    }

    [HttpPatch("me/read-all")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var updated = await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Result = new { updated }
        });
    }

    private async Task WriteEventAsync(string eventName, object data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data, SseJsonOptions);
        await Response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task WriteRawEventAsync(string eventName, string data, CancellationToken cancellationToken)
    {
        await Response.WriteAsync($"event: {eventName}\n", cancellationToken);
        await Response.WriteAsync($"data: {data}\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private async Task WriteHeartbeatAsync(CancellationToken cancellationToken)
    {
        await Response.WriteAsync(": keep-alive\n\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue("userId")
            ?? throw new AppException("UNAUTHENTICATED", "Missing userId claim.", System.Net.HttpStatusCode.Unauthorized);
    }
}


