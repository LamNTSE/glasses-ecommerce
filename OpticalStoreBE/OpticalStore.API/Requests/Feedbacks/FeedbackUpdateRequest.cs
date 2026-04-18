namespace OpticalStore.API.Requests.Feedbacks;

public sealed class FeedbackUpdateRequest
{
    public int? Rating { get; set; }

    public string? Comment { get; set; }
}
