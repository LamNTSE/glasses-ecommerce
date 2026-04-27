using System.ComponentModel.DataAnnotations;

namespace OpticalStore.API.Requests.Auth;

public sealed class TokenRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;
}
