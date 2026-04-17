using System.ComponentModel.DataAnnotations;

namespace OpticalStore.API.Requests.Policies;

public sealed class PolicyUpsertRequest
{
    [Required]
    public string Code { get; set; } = string.Empty;

    [Required]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly? EffectiveFrom { get; set; }

    public DateOnly? EffectiveTo { get; set; }
}
