using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Policies;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IPolicyService
{
    Task<PolicyResponseDto> CreateAsync(string managerUserId, PolicyUpsertDto request, CancellationToken cancellationToken = default);

    Task<PolicyResponseDto> UpdateAsync(int id, PolicyUpsertDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);

    Task<PolicyResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<PagedResultDto<PolicyResponseDto>> GetAllAsync(
        string? keyword,
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        int page,
        int size,
        string sortBy,
        string sortDir,
        CancellationToken cancellationToken = default);
}
