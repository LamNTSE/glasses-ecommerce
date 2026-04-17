namespace OpticalStore.BLL.DTOs.Policies;

public sealed class PolicyResponseDto
{
    public int Id { get; set; }

    public string ManagerUserId { get; set; } = string.Empty;

    public string? ManagerUsername { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }

    public DateTime? CreatedAt { get; set; }
}
