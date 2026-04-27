using OpticalStore.DAL.Entities;

namespace OpticalStore.DAL.Repositories.Interfaces;

public interface IInvalidatedTokenRepository
{
    Task<bool> ExistsAsync(string tokenId, CancellationToken cancellationToken = default);

    Task AddAsync(InvalidatedToken token, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
