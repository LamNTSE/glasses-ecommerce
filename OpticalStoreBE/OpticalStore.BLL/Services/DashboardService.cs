using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Dashboard;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;

namespace OpticalStore.BLL.Services;

public sealed class DashboardService : IDashboardService
{
    private static readonly string[] ActiveOrderStatuses =
    [
        "PENDING",
        "CONFIRMED",
        "WAITING_STOCK",
        "IN_PRODUCTION",
        "READY_TO_SHIP",
        "DELIVERING"
    ];

    private readonly OpticalStoreDbContext _dbContext;

    public DashboardService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardRevenueDto> GetRevenueDashboardAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var nextMonthStart = currentMonthStart.AddMonths(1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        var revenue = await _dbContext.Payments
            .Where(x => x.Status == "PAID")
            .SumAsync(x => x.Amount ?? 0m, cancellationToken);

        var currentMonthRevenue = await _dbContext.Payments
            .Where(x => x.Status == "PAID" && x.PaymentDate.HasValue && x.PaymentDate.Value >= currentMonthStart && x.PaymentDate.Value < nextMonthStart)
            .SumAsync(x => x.Amount ?? 0m, cancellationToken);

        var previousMonthRevenue = await _dbContext.Payments
            .Where(x => x.Status == "PAID" && x.PaymentDate.HasValue && x.PaymentDate.Value >= previousMonthStart && x.PaymentDate.Value < currentMonthStart)
            .SumAsync(x => x.Amount ?? 0m, cancellationToken);

        var growth = previousMonthRevenue == 0m
            ? (currentMonthRevenue > 0m ? 100d : 0d)
            : (double)((currentMonthRevenue - previousMonthRevenue) / previousMonthRevenue * 100m);

        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var activeOrders = await _dbContext.Orders.LongCountAsync(x => x.Status != null && ActiveOrderStatuses.Contains(x.Status), cancellationToken);
        var ordersToday = await _dbContext.Orders.LongCountAsync(x => x.CreatedAt == today, cancellationToken);
        var returnPending = await _dbContext.Orders.LongCountAsync(x => x.Status == "PENDING", cancellationToken);
        var lowStockItems = await _dbContext.Inventories.LongCountAsync(x => (x.Quantity ?? 0) < 10, cancellationToken);

        return new DashboardRevenueDto
        {
            Revenue = revenue,
            RevenueGrowth = growth,
            ActiveOrders = activeOrders,
            OrdersToday = ordersToday,
            ReturnPending = returnPending,
            LowStockItems = lowStockItems
        };
    }
}
