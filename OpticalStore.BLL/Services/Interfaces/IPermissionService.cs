using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Permissions;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IPermissionService
{
    Task<PermissionDto> CreateAsync(CreatePermissionDto request, CancellationToken cancellationToken = default);

    Task<List<PermissionDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(string permissionName, CancellationToken cancellationToken = default);
}
