namespace OpticalStore.API.Requests.Combos;

public sealed class ComboValidateRequest
{
    public string ComboId { get; set; } = string.Empty;

    public List<CartItemRequest> CartItems { get; set; } = new();
}
