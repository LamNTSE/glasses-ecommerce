namespace OpticalStore.API.Requests.Combos;

public sealed class ComboItemRequest
{
    public string? ProductId { get; set; }

    public string? SkuId { get; set; }

    public int? RequiredQuantity { get; set; }
}
