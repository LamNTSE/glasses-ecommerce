namespace OpticalStore.BLL.DTOs.Roles;

public sealed class CreateRoleDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> Permissions { get; set; } = new();
}
