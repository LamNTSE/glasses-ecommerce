using Microsoft.EntityFrameworkCore;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;
using OpticalStore.DAL.Repositories.Interfaces;

namespace OpticalStore.DAL.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly OpticalStoreDbContext _dbContext;

    public RoleRepository(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Role?> GetByNameWithPermissionsAsync(string roleName, CancellationToken cancellationToken = default)
    {
        return _dbContext.Roles
            .Include(x => x.PermissionsNames)
            .FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);
    }
}
