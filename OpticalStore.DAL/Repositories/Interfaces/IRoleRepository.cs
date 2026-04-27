using OpticalStore.DAL.Entities;

namespace OpticalStore.DAL.Repositories.Interfaces;

public interface IRoleRepository
{
    Task<Role?> GetByNameWithPermissionsAsync(string roleName, CancellationToken cancellationToken = default);
}
