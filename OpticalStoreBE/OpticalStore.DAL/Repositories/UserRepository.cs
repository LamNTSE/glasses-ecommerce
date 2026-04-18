using Microsoft.EntityFrameworkCore;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;
using OpticalStore.DAL.Repositories.Interfaces;

namespace OpticalStore.DAL.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly OpticalStoreDbContext _dbContext;

    public UserRepository(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AnyAsync(x => x.Username == username, cancellationToken);
    }

    public Task<User?> GetByUsernameWithSecurityAsync(string username, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .Include(x => x.RoleNames)
            .ThenInclude(x => x.PermissionsNames)
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);
    }

    public Task<User?> GetByIdWithSecurityAsync(string userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .Include(x => x.RoleNames)
            .ThenInclude(x => x.PermissionsNames)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    public Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
    }

    public Task<List<User>> GetUsersWithSecurityAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .Include(x => x.RoleNames)
            .ThenInclude(x => x.PermissionsNames)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        return _dbContext.Users.AddAsync(user, cancellationToken).AsTask();
    }

    public void Remove(User user)
    {
        _dbContext.Users.Remove(user);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
