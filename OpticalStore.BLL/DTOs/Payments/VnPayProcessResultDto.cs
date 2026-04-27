namespace OpticalStore.BLL.DTOs.Payments;

public sealed class VnPayProcessResultDto
{
    public string RspCode { get; set; } = "99";

    public string Message { get; set; } = "Input data required";

    public bool IsSuccessful { get; set; }

    public string RedirectUrl { get; set; } = string.Empty;
}