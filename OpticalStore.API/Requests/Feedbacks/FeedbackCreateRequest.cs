namespace OpticalStore.API.Requests.Feedbacks;

public sealed class FeedbackCreateRequest
{
    public string OrderId { get; set; } = string.Empty;

    public string ProductId { get; set; } = string.Empty;

    public int Rating { get; set; }

    public string? Comment { get; set; }
}
