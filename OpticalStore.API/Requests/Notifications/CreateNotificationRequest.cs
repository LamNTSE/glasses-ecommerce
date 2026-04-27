using System.ComponentModel.DataAnnotations;

namespace OpticalStore.API.Requests.Notifications;

public sealed class CreateNotificationRequest
{
    [Required]
    public string RecipientId { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    [Required]
    public string Content { get; set; } = string.Empty;
}
