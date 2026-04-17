using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Requests.Permissions;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Permissions;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("permissions")]
[Tags("04. Permissions")]
[Authorize(Roles = "ADMIN")]
public sealed class PermissionsController : ControllerBase
{
    private readonly IPermissionService _permissionService;

    public PermissionsController(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PermissionDto>>> Create([FromBody] CreatePermissionRequest request, CancellationToken cancellationToken)
    {
        var result = await _permissionService.CreateAsync(new CreatePermissionDto
        {
            Name = request.Name,
            Description = request.Description
        }, cancellationToken);

        return Ok(new ApiResponse<PermissionDto> { Result = result });
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PermissionDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _permissionService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<List<PermissionDto>> { Result = result });
    }

    [HttpDelete("{permissionName}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string permissionName, CancellationToken cancellationToken)
    {
        await _permissionService.DeleteAsync(permissionName, cancellationToken);
        return Ok(new ApiResponse<object>
        {
            Message = "Permission deleted successfully",
            Result = null
        });
    }
}
