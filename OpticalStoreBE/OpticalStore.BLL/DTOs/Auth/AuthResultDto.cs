namespace OpticalStore.BLL.DTOs.Auth;

public sealed class AuthResultDto
{
    public string Token { get; set; } = string.Empty;

    public bool Authenticated { get; set; }
}
