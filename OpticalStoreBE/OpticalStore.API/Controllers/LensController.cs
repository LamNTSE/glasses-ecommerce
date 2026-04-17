using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Requests.Lenses;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Lenses;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("lenses")]
[Tags("7. Lenses")]
public sealed class LensController : ControllerBase
{
    private readonly ILensService _lensService;

    public LensController(ILensService lensService)
    {
        _lensService = lensService;
    }

    [HttpPost]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<LensResponseDto>>> Create([FromBody] CreateLensRequest request, CancellationToken cancellationToken)
    {
        var result = await _lensService.CreateAsync(new CreateLensDto
        {
            Name = request.Name,
            Material = request.Material,
            Price = request.Price,
            Description = request.Description
        }, cancellationToken);

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
}


