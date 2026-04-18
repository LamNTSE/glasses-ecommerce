using Microsoft.EntityFrameworkCore;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;
using OpticalStore.DAL.Repositories.Interfaces;

namespace OpticalStore.DAL.Repositories;

public sealed class InvalidatedTokenRepository : IInvalidatedTokenRepository
{
    private readonly OpticalStoreDbContext _dbContext;

    public InvalidatedTokenRepository(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> ExistsAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        return _dbContext.InvalidatedTokens.AnyAsync(x => x.Id == tokenId, cancellationToken);
    }

    public Task AddAsync(InvalidatedToken token, CancellationToken cancellationToken = default)
    {
        return _dbContext.InvalidatedTokens.AddAsync(token, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }
}
