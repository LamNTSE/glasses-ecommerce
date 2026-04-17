using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Requests.Policies;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Policies;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("api/policies")]
[Tags("10. Policies")]
public sealed class PolicyController : ControllerBase
{
    private readonly IPolicyService _policyService;

    public PolicyController(IPolicyService policyService)
    {
        _policyService = policyService;
    }

    [HttpPost]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<PolicyResponseDto>>> Create([FromBody] PolicyUpsertRequest request, CancellationToken cancellationToken)
    {
        var managerUserId = User.FindFirstValue("userId") ?? string.Empty;
        var result = await _policyService.CreateAsync(managerUserId, MapRequest(request), cancellationToken);

        return Ok(new ApiResponse<PolicyResponseDto>
        {
            Message = "Tạo policy thành công",
            Result = result
        });
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<PolicyResponseDto>>> Update(int id, [FromBody] PolicyUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await _policyService.UpdateAsync(id, MapRequest(request), cancellationToken);
        return Ok(new ApiResponse<PolicyResponseDto> { Result = result });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(int id, CancellationToken cancellationToken)
    {
        await _policyService.DeleteAsync(id, cancellationToken);
        return Ok(new ApiResponse<object>
        {
            Message = "Xóa policy thành công",
            Result = null
        });
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PolicyResponseDto>>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _policyService.GetByIdAsync(id, cancellationToken);
        return Ok(new ApiResponse<PolicyResponseDto> { Result = result });
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PagedResultDto<PolicyResponseDto>>>> GetAll(
        [FromQuery] string? keyword,
        [FromQuery] DateOnly? effectiveFrom,
        [FromQuery] DateOnly? effectiveTo,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "asc",
        CancellationToken cancellationToken = default)
    {
        var result = await _policyService.GetAllAsync(keyword, effectiveFrom, effectiveTo, page, size, sortBy, sortDir, cancellationToken);
        return Ok(new ApiResponse<PagedResultDto<PolicyResponseDto>> { Result = result });
    }

    private static PolicyUpsertDto MapRequest(PolicyUpsertRequest request)
    {
        return new PolicyUpsertDto
        {
            Code = request.Code,
            Title = request.Title,
            Description = request.Description,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo
        };
    }
}


