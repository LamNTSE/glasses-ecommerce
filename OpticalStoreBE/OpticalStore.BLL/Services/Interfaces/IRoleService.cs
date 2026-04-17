using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Roles;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IRoleService
{
    Task<RoleDto> CreateAsync(CreateRoleDto request, CancellationToken cancellationToken = default);

    Task<List<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(string roleName, CancellationToken cancellationToken = default);
}
