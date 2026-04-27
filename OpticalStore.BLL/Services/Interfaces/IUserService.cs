using OpticalStore.BLL.DTOs.Users;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IUserService
{
    Task<UserResponseDto> RegisterAsync(UserRegistrationDto request, CancellationToken cancellationToken = default);

    Task<List<UserResponseDto>> GetUsersAsync(string role, CancellationToken cancellationToken = default);

    Task<UserResponseDto> GetMyProfileAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserResponseDto> GetByIdAsync(string userId, CancellationToken cancellationToken = default);

    Task<UserResponseDto> UpdateMyProfileAsync(string userId, UserUpdateDto request, CancellationToken cancellationToken = default);

    Task<UserResponseDto> UpdateStatusAsync(string userId, string status, CancellationToken cancellationToken = default);

    Task<UserResponseDto> UpdateRoleAsync(string userId, string role, CancellationToken cancellationToken = default);

    Task DeleteAsync(string userId, CancellationToken cancellationToken = default);
}
