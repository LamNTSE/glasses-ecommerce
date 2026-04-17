using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Dashboard;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("dashboard")]
[Tags("16. Dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("revenue")]
    [Authorize(Roles = "MANAGER,ADMIN")]
    public async Task<ActionResult<ApiResponse<DashboardRevenueDto>>> GetRevenue(CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetRevenueDashboardAsync(cancellationToken);
        return Ok(new ApiResponse<DashboardRevenueDto> { Result = result });
    }
}
