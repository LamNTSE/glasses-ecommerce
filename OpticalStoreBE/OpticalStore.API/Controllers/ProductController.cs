using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Requests.Products;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Products;
using OpticalStore.BLL.DTOs.ProductVariants;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("products")]
[Tags("5. Products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductResponseDto>>> Create(
        [FromForm] string product,
        List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<ProductRequest>(product, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ArgumentException("Invalid product payload.");

        var imageUrls = files?.Select(x => $"local://product/{Guid.NewGuid():N}-{x.FileName}").ToList() ?? new List<string>();

        var result = await _productService.CreateAsync(new ProductUpsertDto
        {
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
            Status = request.Status
        }, imageUrls, cancellationToken);

        return Ok(new ApiResponse<ProductResponseDto>
        {
            Message = "Product created successfully",
            Result = result
        });
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductResponseDto>>> GetById(string id, CancellationToken cancellationToken)
    {
        var result = await _productService.GetByIdAsync(id, cancellationToken);
        return Ok(new ApiResponse<ProductResponseDto> { Result = result });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<ProductResponseDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _productService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<List<ProductResponseDto>> { Result = result });
    }

    [HttpGet("filter")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResultDto<ProductResponseDto>>>> Filter(
        [FromQuery] string? q,
        [FromQuery] string? brand,
        [FromQuery] string? category,
        [FromQuery] string? frameType,
        [FromQuery] string? gender,
        [FromQuery] string? shape,
        [FromQuery] string? frameMaterial,
        [FromQuery] string? hingeType,
        [FromQuery] string? nosePadType,
        [FromQuery] decimal? minWeightGram,
        [FromQuery] decimal? maxWeightGram,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? status,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "name",
        [FromQuery] string sortDir = "asc",
        CancellationToken cancellationToken = default)
    {
        var result = await _productService.FilterAsync(
            q, brand, category, frameType, gender, shape, frameMaterial, hingeType, nosePadType,
            minWeightGram, maxWeightGram, minPrice, maxPrice, status, page, size, sortBy, sortDir, cancellationToken);

        return Ok(new ApiResponse<PagedResultDto<ProductResponseDto>> { Result = result });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<ProductResponseDto>>> Update(string id, [FromBody] ProductRequest request, CancellationToken cancellationToken)
    {
        var result = await _productService.UpdateAsync(id, new ProductUpsertDto
        {
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
            Status = request.Status
        }, cancellationToken);

        return Ok(new ApiResponse<ProductResponseDto> { Result = result });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id, CancellationToken cancellationToken)
    {
        await _productService.DeleteAsync(id, cancellationToken);
        return Ok(new ApiResponse<object>
        {
            Message = "Product deleted successfully",
            Result = null
        });
    }

    [HttpPost("{productId}/images")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<ProductImageDto>>>> UploadImages(
        string productId,
        List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        var imageUrls = files.Select(x => $"local://product/{Guid.NewGuid():N}-{x.FileName}").ToList();
        var result = await _productService.UploadImagesAsync(productId, imageUrls, cancellationToken);
        return Ok(new ApiResponse<List<ProductImageDto>>
        {
            Message = "Uploaded successfully",
            Result = result
        });
    }

    [HttpDelete("images/{imageId}")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteImage(string imageId, CancellationToken cancellationToken)
    {
        await _productService.DeleteImageAsync(imageId, cancellationToken);
        return Ok(new ApiResponse<object>
        {
            Message = "Deleted image successfully",
            Result = null
        });
    }

    [HttpGet("{productId}/variants")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<PagedResultDto<ProductVariantDto>>>> GetVariants(
        string productId,
        [FromQuery] string? q,
        [FromQuery] string? colorName,
        [FromQuery] string? frameFinish,
        [FromQuery] string? sizeLabel,
        [FromQuery] int? lensWidthMm,
        [FromQuery] int? bridgeWidthMm,
        [FromQuery] int? templeLengthMm,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] string? status,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "id",
        [FromQuery] string sortDir = "asc",
        [FromServices] IProductVariantService productVariantService = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await productVariantService.GetByProductIdAsync(
            productId, q, colorName, frameFinish, sizeLabel, lensWidthMm, bridgeWidthMm, templeLengthMm,
            minPrice, maxPrice, status, page, size, sortBy, sortDir, cancellationToken);

        return Ok(new ApiResponse<PagedResultDto<ProductVariantDto>> { Result = result });
    }

    [HttpPost("{productId}/model")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<ProductResponseDto>>> UploadModel(
        string productId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var modelUrl = $"local://model/{Guid.NewGuid():N}-{file.FileName}";
        var result = await _productService.UploadModelAsync(productId, modelUrl, cancellationToken);
        return Ok(new ApiResponse<ProductResponseDto> { Result = result });
    }
}
