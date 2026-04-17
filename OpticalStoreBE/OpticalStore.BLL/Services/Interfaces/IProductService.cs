using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Products;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IProductService
{
    Task<ProductResponseDto> CreateAsync(ProductUpsertDto request, List<string> imageUrls, CancellationToken cancellationToken = default);

    Task<ProductResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<List<ProductResponseDto>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PagedResultDto<ProductResponseDto>> FilterAsync(
        string? q,
        string? brand,
        string? category,
        string? frameType,
        string? gender,
        string? shape,
        string? frameMaterial,
        string? hingeType,
        string? nosePadType,
        decimal? minWeightGram,
        decimal? maxWeightGram,
        decimal? minPrice,
        decimal? maxPrice,
        string? status,
        int page,
        int size,
        string? sortBy,
        string? sortDir,
        CancellationToken cancellationToken = default);

    Task<ProductResponseDto> UpdateAsync(string id, ProductUpsertDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<List<ProductImageDto>> UploadImagesAsync(string productId, List<string> imageUrls, CancellationToken cancellationToken = default);

    Task DeleteImageAsync(string imageId, CancellationToken cancellationToken = default);

    Task<ProductResponseDto> UploadModelAsync(string productId, string modelUrl, CancellationToken cancellationToken = default);
}
