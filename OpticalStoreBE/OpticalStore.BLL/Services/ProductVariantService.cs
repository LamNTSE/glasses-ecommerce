using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.ProductVariants;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class ProductVariantService : IProductVariantService
{
    private readonly OpticalStoreDbContext _dbContext;

    public ProductVariantService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductVariantDto> CreateAsync(ProductVariantUpsertDto request, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(x => x.Id == request.ProductId && x.IsDeleted != true, cancellationToken);
        if (product is null)
        {
            throw new AppException("PRODUCT_NOT_FOUND", "Product not found.", HttpStatusCode.NotFound);
        }

        var variant = await _dbContext.ProductVariants
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(x => x.ProductId == request.ProductId && x.ColorName == request.ColorName && x.SizeLabel == request.SizeLabel && x.IsDeleted != true, cancellationToken);

        if (variant is null)
        {
            variant = new ProductVariant
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = request.ProductId,
                ColorName = request.ColorName,
                FrameFinish = request.FrameFinish,
                LensWidthMm = request.LensWidthMm,
                BridgeWidthMm = request.BridgeWidthMm,
                TempleLengthMm = request.TempleLengthMm,
                SizeLabel = request.SizeLabel,
                Price = request.Price,
                Status = request.Status,
                OrderItemType = ResolveOrderItemType(request.Quantity, null),
                IsDeleted = false,
                // Quantity removed from ProductVariant; inventory.Quantity will be used
            };
            _dbContext.ProductVariants.Add(variant);
        }
        else
        {
            // do not maintain Quantity on ProductVariant; inventory holds stock
            variant.OrderItemType = ResolveOrderItemType(request.Quantity, variant.Inventory);
        }

        if (variant.Inventory is null)
        {
            variant.Inventory = new Inventory
            {
                Id = Guid.NewGuid().ToString(),
                Quantity = request.Quantity ?? 0,
                ReservedQuantity = 0,
                ProductVariantId = variant.Id
            };
        }
        else if (request.Quantity.HasValue)
        {
            EnsureNewQuantityRespectsReserved(request.Quantity.Value, variant.Inventory.ReservedQuantity);
            variant.Inventory.Quantity = request.Quantity;
        }

        variant.OrderItemType = ResolveOrderItemType(request.Quantity, variant.Inventory);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(variant.Id, cancellationToken);
    }

    public async Task<ProductVariantDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted != true, cancellationToken);

        if (variant is null)
        {
            throw new AppException("PRODUCT_VARIANT_NOT_FOUND", "Product variant not found.", HttpStatusCode.NotFound);
        }

        return Map(variant);
    }

    public async Task<ProductVariantDto> UpdateAsync(string id, ProductVariantUpsertDto request, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted != true, cancellationToken);

        if (variant is null)
        {
            throw new AppException("PRODUCT_VARIANT_NOT_FOUND", "Product variant not found.", HttpStatusCode.NotFound);
        }

        variant.ColorName = request.ColorName;
        variant.FrameFinish = request.FrameFinish;
        variant.LensWidthMm = request.LensWidthMm;
        variant.BridgeWidthMm = request.BridgeWidthMm;
        variant.TempleLengthMm = request.TempleLengthMm;
        variant.SizeLabel = request.SizeLabel;
        variant.Price = request.Price;
        variant.Status = request.Status;
        variant.OrderItemType = request.OrderItemType;

        // update inventory quantity if provided
        if (variant.Inventory is not null && request.Quantity.HasValue)
        {
            EnsureNewQuantityRespectsReserved(request.Quantity.Value, variant.Inventory.ReservedQuantity);
            variant.Inventory.Quantity = request.Quantity;
        }

        variant.OrderItemType = ResolveOrderItemType(request.Quantity, variant.Inventory);

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(variant);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants.FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted != true, cancellationToken);
        if (variant is null)
        {
            throw new AppException("PRODUCT_VARIANT_NOT_FOUND", "Product variant not found.", HttpStatusCode.NotFound);
        }

        variant.IsDeleted = true;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InventoryUpdateResultDto> UpdateInventoryAsync(InventoryUpdateDto request, CancellationToken cancellationToken = default)
    {
        var variant = await _dbContext.ProductVariants
            .Include(x => x.Inventory)
            .FirstOrDefaultAsync(x => x.Id == request.ProductVariantId && x.IsDeleted != true, cancellationToken);

        if (variant is null)
        {
            throw new AppException("PRODUCT_VARIANT_NOT_FOUND", "Product variant not found.", HttpStatusCode.NotFound);
        }

        if (variant.Inventory is null)
        {
            variant.Inventory = new Inventory
            {
                Id = Guid.NewGuid().ToString(),
                ProductVariantId = variant.Id,
                Quantity = request.ChangeAmount,
                ReservedQuantity = 0
            };
        }
        else
        {
            EnsureNewQuantityRespectsReserved(request.ChangeAmount, variant.Inventory.ReservedQuantity);
            variant.Inventory.Quantity = request.ChangeAmount;
        }

        variant.OrderItemType = ResolveOrderItemType(null, variant.Inventory);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new InventoryUpdateResultDto
        {
            ProductVariant = Map(variant),
            UpdatedOrderCount = 0,
            UpdatedOrders = new List<object>()
        };
    }

    public async Task<PagedResultDto<ProductVariantDto>> GetByProductIdAsync(
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
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ProductVariants
            .Include(x => x.Inventory)
            .Where(x => x.ProductId == productId && x.IsDeleted != true);

        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => (x.ColorName ?? string.Empty).ToLower().Contains(q.ToLower()));
        if (!string.IsNullOrWhiteSpace(colorName)) query = query.Where(x => x.ColorName == colorName);
        if (!string.IsNullOrWhiteSpace(frameFinish)) query = query.Where(x => x.FrameFinish == frameFinish);
        if (!string.IsNullOrWhiteSpace(sizeLabel)) query = query.Where(x => x.SizeLabel == sizeLabel);
        if (lensWidthMm.HasValue) query = query.Where(x => x.LensWidthMm == lensWidthMm);
        if (bridgeWidthMm.HasValue) query = query.Where(x => x.BridgeWidthMm == bridgeWidthMm);
        if (templeLengthMm.HasValue) query = query.Where(x => x.TempleLengthMm == templeLengthMm);
        if (minPrice.HasValue) query = query.Where(x => x.Price >= minPrice);
        if (maxPrice.HasValue) query = query.Where(x => x.Price <= maxPrice);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);

        var total = await query.LongCountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(total / (double)Math.Max(1, size));

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var key = (sortBy ?? "id").ToLowerInvariant();
        query = key switch
        {
            "price" => desc ? query.OrderByDescending(x => x.Price) : query.OrderBy(x => x.Price),
            _ => desc ? query.OrderByDescending(x => x.Id) : query.OrderBy(x => x.Id)
        };

        var data = await query.Skip(page * size).Take(size).ToListAsync(cancellationToken);

        return new PagedResultDto<ProductVariantDto>
        {
            Items = data.Select(Map).ToList(),
            Page = page,
            Size = size,
            TotalElements = total,
            TotalPages = totalPages
        };
    }

    private static ProductVariantDto Map(ProductVariant variant)
    {
        return new ProductVariantDto
        {
            Id = variant.Id,
            ProductId = variant.ProductId,
            ColorName = variant.ColorName,
            FrameFinish = variant.FrameFinish,
            LensWidthMm = variant.LensWidthMm,
            BridgeWidthMm = variant.BridgeWidthMm,
            TempleLengthMm = variant.TempleLengthMm,
            SizeLabel = variant.SizeLabel,
            Price = variant.Price,
            Quantity = variant.Inventory?.Quantity,
            Status = variant.Status,
            OrderItemType = ResolveOrderItemType(null, variant.Inventory)
        };
    }

    private static string ResolveOrderItemType(int? quantityFallback, Inventory? inventory)
    {
        var available = inventory is null
            ? (quantityFallback ?? 0)
            : (inventory.Quantity ?? 0) - (inventory.ReservedQuantity ?? 0);

        return available > 0 ? "IN_STOCK" : "PRE_ORDER";
    }

    private static void EnsureNewQuantityRespectsReserved(int newQuantity, int? reserved)
    {
        var r = reserved ?? 0;
        if (newQuantity < r)
        {
            throw new AppException(
                "INVALID_INVENTORY",
                $"Số lượng tồn ({newQuantity}) phải lớn hơn hoặc bằng số lượng đang giữ chỗ (reserved) ({r}).",
                HttpStatusCode.BadRequest);
        }
    }
}
