using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.ProductVariants;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IProductVariantService
{
    Task<ProductVariantDto> CreateAsync(ProductVariantUpsertDto request, CancellationToken cancellationToken = default);

    Task<ProductVariantDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<ProductVariantDto> UpdateAsync(string id, ProductVariantUpsertDto request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<InventoryUpdateResultDto> UpdateInventoryAsync(InventoryUpdateDto request, CancellationToken cancellationToken = default);

    Task<PagedResultDto<ProductVariantDto>> GetByProductIdAsync(
        string productId,
        string? q,
        string? colorName,
        string? frameFinish,
        string? sizeLabel,
        int? lensWidthMm,
        int? bridgeWidthMm,
        int? templeLengthMm,
        decimal? minPrice,
        decimal? maxPrice,
        string? status,
        int page,
        int size,
        string? sortBy,
        string? sortDir,
        CancellationToken cancellationToken = default);
}
