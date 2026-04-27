using System.ComponentModel.DataAnnotations;

namespace OpticalStore.API.Requests.Auth;

public sealed class LoginRequest
{
    [Required]
    [MinLength(3)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;
}
