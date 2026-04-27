using OpticalStore.BLL.DTOs.Common;

namespace OpticalStore.BLL.DTOs.Users;

public sealed class UserResponseDto
{
    public string Id { get; set; } = string.Empty;

    public string? Username { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public DateOnly? Dob { get; set; }

    public string? ImageUrl { get; set; }

    public string? Email { get; set; }

    public string? Phone { get; set; }

    public List<RoleDto> Roles { get; set; } = new();
}
