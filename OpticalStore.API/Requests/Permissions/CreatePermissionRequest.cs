namespace OpticalStore.API.Requests.Permissions;

public sealed class CreatePermissionRequest
{
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
