using OpticalStore.DAL.Entities;

namespace OpticalStore.DAL.Repositories.Interfaces;

public interface IUserRepository
{
    Task<bool> ExistsByUsernameAsync(string username, CancellationToken cancellationToken = default);

    Task<User?> GetByUsernameWithSecurityAsync(string username, CancellationToken cancellationToken = default);

    Task<User?> GetByIdWithSecurityAsync(string userId, CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<List<User>> GetUsersWithSecurityAsync(CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);

    void Remove(User user);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
