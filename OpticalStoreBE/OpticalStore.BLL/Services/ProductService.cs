using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Products;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class ProductService : IProductService
{
    private readonly OpticalStoreDbContext _dbContext;

    public ProductService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ProductResponseDto> CreateAsync(ProductUpsertDto request, List<string> imageUrls, CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Brand = request.Brand,
            Category = request.Category,
            FrameType = request.FrameType,
            Gender = request.Gender,
            Shape = request.Shape,
            FrameMaterial = request.FrameMaterial,
            HingeType = request.HingeType,
            NosePadType = request.NosePadType,
            WeightGram = request.WeightGram,
            Status = request.Status,
            IsDeleted = false
        };

        foreach (var imageUrl in imageUrls.Take(5))
        {
            product.ProductImages.Add(new ProductImage
            {
                Id = Guid.NewGuid().ToString(),
                ImageUrl = imageUrl
            });
        }

        _dbContext.Products.Add(product);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task<ProductResponseDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var product = await QueryProducts()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (product is null)
        {
            throw new AppException("PRODUCT_NOT_FOUND", "Product not found.", HttpStatusCode.NotFound);
        }

        return Map(product);
    }

    public async Task<List<ProductResponseDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var data = await QueryProducts().ToListAsync(cancellationToken);
        return data.Select(Map).ToList();
    }

    public async Task<PagedResultDto<ProductResponseDto>> FilterAsync(
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
        CancellationToken cancellationToken = default)
    {
        var query = QueryProducts();

        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(x => x.Name.ToLower().Contains(q.ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(brand)) query = query.Where(x => x.Brand == brand);
        if (!string.IsNullOrWhiteSpace(category)) query = query.Where(x => x.Category == category);
        if (!string.IsNullOrWhiteSpace(frameType)) query = query.Where(x => x.FrameType == frameType);
        if (!string.IsNullOrWhiteSpace(gender)) query = query.Where(x => x.Gender == gender);
        if (!string.IsNullOrWhiteSpace(shape)) query = query.Where(x => x.Shape == shape);
        if (!string.IsNullOrWhiteSpace(frameMaterial)) query = query.Where(x => x.FrameMaterial == frameMaterial);
        if (!string.IsNullOrWhiteSpace(hingeType)) query = query.Where(x => x.HingeType == hingeType);
        if (!string.IsNullOrWhiteSpace(nosePadType)) query = query.Where(x => x.NosePadType == nosePadType);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        if (minWeightGram.HasValue) query = query.Where(x => x.WeightGram >= minWeightGram);
        if (maxWeightGram.HasValue) query = query.Where(x => x.WeightGram <= maxWeightGram);
        if (minPrice.HasValue) query = query.Where(x => x.ProductVariants.Any(v => v.Price >= minPrice));
        if (maxPrice.HasValue) query = query.Where(x => x.ProductVariants.Any(v => v.Price <= maxPrice));

        var total = await query.LongCountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(total / (double)Math.Max(1, size));

        query = ApplySort(query, sortBy, sortDir);
        var data = await query.Skip(page * size).Take(size).ToListAsync(cancellationToken);

        return new PagedResultDto<ProductResponseDto>
        {
            Items = data.Select(Map).ToList(),
            Page = page,
            Size = size,
            TotalElements = total,
            TotalPages = totalPages
        };
    }

    public async Task<ProductResponseDto> UpdateAsync(string id, ProductUpsertDto request, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted != true, cancellationToken);
        if (product is null)
        {
            throw new AppException("PRODUCT_NOT_FOUND", "Product not found.", HttpStatusCode.NotFound);
        }

        product.Name = request.Name;
        product.Brand = request.Brand;
        product.Category = request.Category;
        product.FrameType = request.FrameType;
        product.Gender = request.Gender;
        product.Shape = request.Shape;
        product.FrameMaterial = request.FrameMaterial;
        product.HingeType = request.HingeType;
        product.NosePadType = request.NosePadType;
        product.WeightGram = request.WeightGram;
        product.Status = request.Status;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.Include(x => x.ProductVariants)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted != true, cancellationToken);

        if (product is null)
        {
            throw new AppException("PRODUCT_NOT_FOUND", "Product not found.", HttpStatusCode.NotFound);
        }

        product.IsDeleted = true;
        foreach (var variant in product.ProductVariants)
        {
            variant.IsDeleted = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ReplaceImagesAsync(string productId, List<string> imageUrls, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.Include(x => x.ProductImages)
            .FirstOrDefaultAsync(x => x.Id == productId && x.IsDeleted != true, cancellationToken);

        if (product is null)
            throw new AppException("PRODUCT_NOT_FOUND", "Product not found.", System.Net.HttpStatusCode.NotFound);

        if (imageUrls.Count > 5)
            throw new AppException("IMAGE_LIMIT_EXCEEDED", "Maximum 5 images per product.", System.Net.HttpStatusCode.BadRequest);

        _dbContext.ProductImages.RemoveRange(product.ProductImages);

        foreach (var url in imageUrls)
        {
            _dbContext.ProductImages.Add(new ProductImage
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = product.Id,
                ImageUrl = url
            });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<ProductImageDto>> UploadImagesAsync(string productId, List<string> imageUrls, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.Include(x => x.ProductImages)
            .FirstOrDefaultAsync(x => x.Id == productId && x.IsDeleted != true, cancellationToken);

        if (product is null)
        {
            throw new AppException("PRODUCT_NOT_FOUND", "Product not found.", HttpStatusCode.NotFound);
        }

        if (product.ProductImages.Count + imageUrls.Count > 5)
        {
            throw new AppException("IMAGE_LIMIT_EXCEEDED", "Maximum 5 images per product.", HttpStatusCode.BadRequest);
        }

        var created = new List<ProductImageDto>();
        foreach (var imageUrl in imageUrls)
        {
            var image = new ProductImage
            {
                Id = Guid.NewGuid().ToString(),
                ProductId = product.Id,
                ImageUrl = imageUrl
            };
            _dbContext.ProductImages.Add(image);
            created.Add(new ProductImageDto { Id = image.Id, ImageUrl = image.ImageUrl });
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return created;
    }

    public async Task DeleteImageAsync(string imageId, CancellationToken cancellationToken = default)
    {
        var image = await _dbContext.ProductImages.FirstOrDefaultAsync(x => x.Id == imageId, cancellationToken);
        if (image is null)
        {
            throw new AppException("PRODUCT_IMAGE_NOT_FOUND", "Product image not found.", HttpStatusCode.NotFound);
        }

        _dbContext.ProductImages.Remove(image);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ProductResponseDto> UploadModelAsync(string productId, string modelUrl, CancellationToken cancellationToken = default)
    {
        var product = await _dbContext.Products.FirstOrDefaultAsync(x => x.Id == productId && x.IsDeleted != true, cancellationToken);
        if (product is null)
        {
            throw new AppException("PRODUCT_NOT_FOUND", "Product not found.", HttpStatusCode.NotFound);
        }

        product.ModelUrl = modelUrl;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(productId, cancellationToken);
    }

    private IQueryable<Product> QueryProducts()
    {
        return _dbContext.Products
            .Include(x => x.ProductImages)
            .Include(x => x.ProductVariants)
                .ThenInclude(x => x.Inventory)
            .Where(x => x.IsDeleted != true);
    }

    private static IQueryable<Product> ApplySort(IQueryable<Product> query, string? sortBy, string? sortDir)
    {
        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var key = (sortBy ?? "name").ToLowerInvariant();

        return key switch
        {
            "brand" => desc ? query.OrderByDescending(x => x.Brand) : query.OrderBy(x => x.Brand),
            "status" => desc ? query.OrderByDescending(x => x.Status) : query.OrderBy(x => x.Status),
            _ => desc ? query.OrderByDescending(x => x.Name) : query.OrderBy(x => x.Name)
        };
    }

    private static ProductResponseDto Map(Product product)
    {
        var prices = product.ProductVariants
            .Where(x => x.IsDeleted != true && x.Price.HasValue)
            .Select(x => x.Price!.Value)
            .ToList();

        return new ProductResponseDto
        {
            Id = product.Id,
            Name = product.Name,
            Brand = product.Brand,
            Category = product.Category,
            FrameType = product.FrameType,
            Gender = product.Gender,
            Shape = product.Shape,
            FrameMaterial = product.FrameMaterial,
            HingeType = product.HingeType,
            NosePadType = product.NosePadType,
            WeightGram = product.WeightGram,
            MinPrice = prices.Count == 0 ? null : prices.Min(),
            MaxPrice = prices.Count == 0 ? null : prices.Max(),
            Status = product.Status,
            OrderItemType = ResolveOrderItemType(product.ProductVariants.FirstOrDefault(x => x.IsDeleted != true)),
            ModelUrl = product.ModelUrl,
            ImageUrl = product.ProductImages.Select(x => new ProductImageDto
            {
                Id = x.Id,
                ImageUrl = x.ImageUrl
            }).ToList()
        };
    }

    private static string? ResolveOrderItemType(ProductVariant? variant)
    {
        if (variant is null)
        {
            return null;
        }

        if (variant.Inventory is null)
        {
            return variant.OrderItemType;
        }

        var available = (variant.Inventory.Quantity ?? 0) - (variant.Inventory.ReservedQuantity ?? 0);
        return available > 0 ? "IN_STOCK" : "PRE_ORDER";
    }
}
