using OpticalStore.BLL.DTOs.Dashboard;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IDashboardService
{
    Task<DashboardRevenueDto> GetRevenueDashboardAsync(CancellationToken cancellationToken = default);
}
