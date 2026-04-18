namespace OpticalStore.BLL.DTOs.Feedbacks;

public sealed class FeedbackCreateDto
{
    public string OrderId { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public int Rating { get; set; }

    public string? Comment { get; set; }
}
