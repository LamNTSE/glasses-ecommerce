using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Mappings;
using OpticalStore.API.Requests.Lenses;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Lenses;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("lenses")]
[Tags("07. Lenses")]
public sealed class LensController : ControllerBase
{
    private readonly ILensService _lensService;

    public LensController(ILensService lensService)
    {
        _lensService = lensService;
    }

    [HttpPost]
    [Authorize(Roles = "OPERATION,MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<LensResponseDto>>> Create([FromBody] CreateLensRequest request, CancellationToken cancellationToken)
    {
        var result = await _lensService.CreateAsync(request.ToDto(), cancellationToken);

        return Ok(new ApiResponse<LensResponseDto>
        {
            Message = "Lens created successfully",
            Result = result
        });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<LensResponseDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _lensService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<List<LensResponseDto>> { Result = result });
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LensResponseDto>>> GetById(string id, CancellationToken cancellationToken)
    {
        var result = await _lensService.GetByIdAsync(id, cancellationToken);
        return Ok(new ApiResponse<LensResponseDto> { Result = result });
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "OPERATION,MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<LensResponseDto>>> Update(string id, [FromBody] CreateLensRequest request, CancellationToken cancellationToken)
    {
        var result = await _lensService.UpdateAsync(id, request.ToDto(), cancellationToken);
        return Ok(new ApiResponse<LensResponseDto>
        {
            Message = "Lens updated successfully",
            Result = result
        });
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "OPERATION,MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string id, CancellationToken cancellationToken)
    {
        await _lensService.DeleteAsync(id, cancellationToken);
        return Ok(new ApiResponse<object> { Message = "Lens deleted successfully" });
    }
}


