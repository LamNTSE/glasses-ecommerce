namespace OpticalStore.BLL.DTOs.Common;

public sealed class RoleDto
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<PermissionDto> Permissions { get; set; } = new();
}
