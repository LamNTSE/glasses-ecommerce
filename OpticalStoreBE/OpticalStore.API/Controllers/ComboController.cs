using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Mappings;
using OpticalStore.API.Requests.Combos;
using OpticalStore.API.Responses;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("api/combos")]
[Tags("08. Combos")]
public sealed class ComboController : ControllerBase
{
    private readonly IComboWorkflowService _comboWorkflowService;

    public ComboController(IComboWorkflowService comboWorkflowService)
    {
        _comboWorkflowService = comboWorkflowService;
    }

    [HttpPost]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] ComboUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await _comboWorkflowService.CreateAsync(request.ToDto(), cancellationToken);
        return Ok(new ApiResponse<object> { Message = "Tạo combo thành công", Result = result });
    }

    [HttpPut("{comboId}")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Update(string comboId, [FromBody] ComboUpsertRequest request, CancellationToken cancellationToken)
    {
        var result = await _comboWorkflowService.UpdateAsync(comboId, request.ToDto(), cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPatch("{comboId}/status")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateStatus(string comboId, [FromBody] ComboStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _comboWorkflowService.UpdateStatusAsync(comboId, request.Status, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpGet]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var result = await _comboWorkflowService.GetAllAsync(
            status,
            fromDate,
            toDate,
            page,
            size,
            sortBy,
            sortDir,
            cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Result = result
        });
    }

    [HttpGet("{comboId}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetById(string comboId, CancellationToken cancellationToken)
    {
        var result = await _comboWorkflowService.GetByIdAsync(comboId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpGet("available")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetAvailable([FromQuery] DateTime? currentTime, CancellationToken cancellationToken)
    {
        var results = await _comboWorkflowService.GetAvailableAsync(currentTime, cancellationToken);

        return Ok(new ApiResponse<List<object>> { Result = results });
    }

    [HttpPost("validate")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Validate([FromBody] ComboValidateRequest request, CancellationToken cancellationToken)
    {
        var result = await _comboWorkflowService.ValidateAsync(request.ToDto(), cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPost("check-stock")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> CheckStock([FromQuery] string comboId, CancellationToken cancellationToken)
    {
        var stock = await _comboWorkflowService.CheckStockAsync(comboId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = stock });
    }

    [HttpDelete("{comboId}")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string comboId, CancellationToken cancellationToken)
    {
        await _comboWorkflowService.DeleteAsync(comboId, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Message = "Combo deleted successfully",
            Result = null
        });
    }
}


