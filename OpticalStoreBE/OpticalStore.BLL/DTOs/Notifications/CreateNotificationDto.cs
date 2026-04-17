namespace OpticalStore.BLL.DTOs.Notifications;

public sealed class CreateNotificationDto
{
    public string RecipientId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
