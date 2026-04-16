using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;
using OpticalStore.DAL.Repositories.Interfaces;

namespace OpticalStore.DAL.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly OpticalStoreDbContext _dbContext;

        public UserRepository(OpticalStoreDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u =>
                u.RefreshToken == refreshToken &&
                u.RefreshTokenExpiryTime != null &&
                u.RefreshTokenExpiryTime > DateTime.UtcNow);
        }

        public async Task<User?> GetByIdAsync(string id)
        {
            return await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task AddAsync(User user)
        {
            await _dbContext.Users.AddAsync(user);
        }

        public void Update(User user)
        {
            _dbContext.Users.Update(user);
        }

        public async Task<int> SaveChangesAsync()
        {
            return await _dbContext.SaveChangesAsync();
        }
    }
}
