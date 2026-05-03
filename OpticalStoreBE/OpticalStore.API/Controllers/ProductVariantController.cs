using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Mappings;
using OpticalStore.API.Requests.ProductVariants;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.ProductVariants;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("product-variants")]
[Tags("06. Product Variants")]
public sealed class ProductVariantController : ControllerBase
{
    private readonly IProductVariantService _productVariantService;

    // Khoi tao controller va gan service xu ly variant san pham.
    public ProductVariantController(IProductVariantService productVariantService)
    {
        _productVariantService = productVariantService;
    }

    // Tao variant moi.
    [HttpPost]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductVariantDto>>> Create([FromBody] ProductVariantRequest request, CancellationToken cancellationToken)
    {
        var result = await _productVariantService.CreateAsync(request.ToDto(), cancellationToken);

        return Ok(new ApiResponse<ProductVariantDto> { Result = result });
    }

    // Lay variant theo id.
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductVariantDto>>> GetById(string id, CancellationToken cancellationToken)
    {
        var result = await _productVariantService.GetByIdAsync(id, cancellationToken);
        return Ok(new ApiResponse<ProductVariantDto> { Result = result });
    }

    // Cap nhat thong tin variant.
    [HttpPut("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<ProductVariantDto>>> Update(string id, [FromBody] ProductVariantRequest request, CancellationToken cancellationToken)
    {
        var result = await _productVariantService.UpdateAsync(id, request.ToDto(), cancellationToken);

        return Ok(new ApiResponse<ProductVariantDto> { Result = result });
    }

    // Xoa mem variant.
    [HttpDelete("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id, CancellationToken cancellationToken)
    {
        await _productVariantService.DeleteAsync(id, cancellationToken);
        return Ok(new ApiResponse<object> { Result = null });
    }

    // Cap nhat ton kho cua variant.
    [HttpPatch("inventory")]
    [Authorize(Roles = "OPERATION,MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<InventoryUpdateResultDto>>> UpdateInventory([FromBody] InventoryUpdateRequest request, CancellationToken cancellationToken)
    {
        var result = await _productVariantService.UpdateInventoryAsync(request.ToDto(), cancellationToken);

        return Ok(new ApiResponse<InventoryUpdateResultDto> { Result = result });
    }
}


