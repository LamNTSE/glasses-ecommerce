using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
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
[Tags("05. Products")]
public sealed class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IWebHostEnvironment _environment;

    // Khoi tao controller va gan service xu ly san pham.
    public ProductsController(IProductService productService, IWebHostEnvironment environment)
    {
        _productService = productService;
        _environment = environment;
    }

    // Tao san pham moi va luu anh di kem neu co.
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

        var imageUrls = new List<string>();

        // Uu tien anh upload tu client, gioi han toi da 5 anh.
        if (files is { Count: > 0 })
        {
            foreach (var file in files)
            {
                if (imageUrls.Count >= 5)
                {
                    break;
                }

                imageUrls.Add(await ProductImageStorage.SaveAsync(file, _environment, cancellationToken));
            }
        }


        // Bo sung them imageUrls co san trong payload neu client gui kem.
        if (request.ImageUrls is { Count: > 0 })
        {
            foreach (var url in request.ImageUrls)
            {
                if (imageUrls.Count >= 5)
                {
                    break;
                }

                if (!string.IsNullOrWhiteSpace(url))
                {
                    imageUrls.Add(url.Trim());
                }
            }
        }

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

    // Lay san pham theo id.
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductResponseDto>>> GetById(string id, CancellationToken cancellationToken)
    {
        var result = await _productService.GetByIdAsync(id, cancellationToken);
        return Ok(new ApiResponse<ProductResponseDto> { Result = result });
    }

    // Lay toan bo san pham dang hoat dong.
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<ProductResponseDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _productService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<List<ProductResponseDto>> { Result = result });
    }

    // Loc san pham theo nhieu dieu kien tim kiem.
    [HttpGet("filter")]
    [ApiExplorerSettings(IgnoreApi = true)]
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

    // Cap nhat thong tin san pham va thay anh neu request co gui len.
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

        // Replace images if ImageUrls provided in the request.
        if (request.ImageUrls is not null)
            await _productService.ReplaceImagesAsync(id, request.ImageUrls, cancellationToken);

        return Ok(new ApiResponse<ProductResponseDto> { Result = result });
    }

    // Xoa mem san pham.
    [HttpDelete("{id}")]
    [ApiExplorerSettings(IgnoreApi = true)]
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

    // Tai them anh san pham tu files upload.
    [HttpPost("{productId}/images")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<ProductImageDto>>>> UploadImages(
        string productId,
        List<IFormFile> files,
        CancellationToken cancellationToken)
    {
        var imageUrls = new List<string>();

        // Chuyen tung file thanh duong dan luu tru.
        foreach (var file in files)
        {
            imageUrls.Add(await ProductImageStorage.SaveAsync(file, _environment, cancellationToken));
        }

        var result = await _productService.UploadImagesAsync(productId, imageUrls, cancellationToken);
        return Ok(new ApiResponse<List<ProductImageDto>>
        {
            Message = "Uploaded successfully",
            Result = result
        });
    }

    // Xoa mot anh san pham rieng le.
    [HttpDelete("images/{imageId}")]
    [ApiExplorerSettings(IgnoreApi = true)]
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

    // Lay danh sach variant cua san pham theo cac bo loc.
    [HttpGet("{productId}/variants")]
    [ApiExplorerSettings(IgnoreApi = true)]
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

    // Tai model 3D/asset lien quan cua san pham.
    [HttpPost("{productId}/model")]
    [ApiExplorerSettings(IgnoreApi = true)]
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
