namespace OpticalStore.API.Requests.Combos;

public sealed class ComboUpsertRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string DiscountType { get; set; } = "PERCENT";

    public decimal DiscountValue { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public bool? IsManuallyDisabled { get; set; }

    public List<ComboItemRequest>? ComboItems { get; set; }
}
