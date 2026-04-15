using System.Threading.Tasks;
using OpticalStore.DAL.Entities;

namespace OpticalStore.DAL.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByEmailAsync(string email);
        Task<User?> GetByRefreshTokenAsync(string refreshToken);
        Task<User?> GetByIdAsync(long id);
        Task AddAsync(User user);
        void Update(User user);
        Task<int> SaveChangesAsync();
    }
}
