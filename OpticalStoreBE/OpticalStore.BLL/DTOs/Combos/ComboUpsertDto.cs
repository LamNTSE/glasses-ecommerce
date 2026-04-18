namespace OpticalStore.BLL.DTOs.Combos;

public sealed class ComboUpsertDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string DiscountType { get; set; } = "PERCENT";

    public decimal DiscountValue { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public bool? IsManuallyDisabled { get; set; }

    public List<ComboItemDto>? ComboItems { get; set; }
}
