namespace OpticalStore.BLL.DTOs.Notifications;

public sealed class NotificationResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string RecipientId { get; set; } = string.Empty;

    public string? RecipientName { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public string? SenderId { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReadAt { get; set; }
}
