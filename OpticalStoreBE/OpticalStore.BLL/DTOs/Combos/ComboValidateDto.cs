namespace OpticalStore.BLL.DTOs.Combos;

public sealed class ComboValidateDto
{
    public string ComboId { get; set; } = string.Empty;

    public List<CartItemDto> CartItems { get; set; } = new();
}
