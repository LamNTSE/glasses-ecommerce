using System.Threading.Tasks;
using OpticalStore.BLL.DTOs;

namespace OpticalStore.BLL.Services.Interfaces
{
    public interface IAuthService
    {
        Task RegisterAsync(RegisterRequestDto request);
        Task<AuthResultDto> LoginAsync(LoginRequestDto request);
        Task<AuthResultDto> RefreshTokenAsync(string refreshToken);
        Task RevokeRefreshTokenAsync(string userId);
        Task<UserDto?> GetCurrentUserAsync(string userId);
    }
}
