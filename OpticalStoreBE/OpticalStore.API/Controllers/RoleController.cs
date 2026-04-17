using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Requests.Roles;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Roles;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("roles")]
[Tags("3. Roles")]
[Authorize(Roles = "ADMIN")]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService)
    {
        _roleService = roleService;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<RoleDto>>> Create([FromBody] CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await _roleService.CreateAsync(new CreateRoleDto
        {
            Name = request.Name,
            Description = request.Description,
            Permissions = request.Permissions
        }, cancellationToken);

        return Ok(new ApiResponse<RoleDto> { Result = result });
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<RoleDto>>>> GetAll(CancellationToken cancellationToken)
    {
        var result = await _roleService.GetAllAsync(cancellationToken);
        return Ok(new ApiResponse<List<RoleDto>> { Result = result });
    }

    [HttpDelete("{roleName}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string roleName, CancellationToken cancellationToken)
    {
        await _roleService.DeleteAsync(roleName, cancellationToken);
        return Ok(new ApiResponse<object>
        {
            Message = "Role deleted successfully",
            Result = null
        });
    }
}
