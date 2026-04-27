namespace OpticalStore.BLL.DTOs.Feedbacks;

public sealed class FeedbackResponseDto
{
    public string FeedbackId { get; set; } = string.Empty;

    public string? OrderId { get; set; }

    public string? ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public int? Rating { get; set; }

    public string? Comment { get; set; }

    public List<string> ImageUrls { get; set; } = new();

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
