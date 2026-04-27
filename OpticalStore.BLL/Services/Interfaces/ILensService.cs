using OpticalStore.BLL.DTOs.Lenses;

namespace OpticalStore.BLL.Services.Interfaces;

public interface ILensService
{
    Task<LensResponseDto> CreateAsync(CreateLensDto request, CancellationToken cancellationToken = default);

    Task<List<LensResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<LensResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<LensResponseDto> UpdateAsync(string id, CreateLensDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}
