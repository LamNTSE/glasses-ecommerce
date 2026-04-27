namespace OpticalStore.API.Requests.Roles;

public sealed class CreateRoleRequest
{
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<string> Permissions { get; set; } = new();
}
