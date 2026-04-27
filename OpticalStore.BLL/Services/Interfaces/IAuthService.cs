using OpticalStore.BLL.DTOs.Auth;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IAuthService
{
    Task<AuthResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    Task<IntrospectResultDto> IntrospectAsync(TokenRequestDto request, CancellationToken cancellationToken = default);

    Task<AuthResultDto> RefreshAsync(TokenRequestDto request, CancellationToken cancellationToken = default);

    Task LogoutAsync(TokenRequestDto request, CancellationToken cancellationToken = default);
}
