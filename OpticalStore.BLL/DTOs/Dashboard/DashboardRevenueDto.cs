namespace OpticalStore.BLL.DTOs.Dashboard;

public sealed class DashboardRevenueDto
{
    public decimal Revenue { get; set; }

    public double RevenueGrowth { get; set; }

    public long ActiveOrders { get; set; }

    public long OrdersToday { get; set; }

    public long ReturnPending { get; set; }

    public long LowStockItems { get; set; }
}
