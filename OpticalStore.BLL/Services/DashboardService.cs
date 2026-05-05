using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Dashboard;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class DashboardService : IDashboardService
{
    private const string StatusDelivered = "DELIVERED";
    private const string StatusCompleted = "COMPLETED";
    private const string WindowsVietnamTimeZoneId = "SE Asia Standard Time";
    private const string IanaVietnamTimeZoneId = "Asia/Ho_Chi_Minh";

    private static readonly string[] ActiveOrderStatuses =
    [
        "PENDING",
        "PAID",
        "AWAITING_VERIFICATION",
        "PREPARING",
        "CONFIRMED",
        "PREORDER_CONFIRMED",
        "STOCK_READY",
        "IN_PRODUCTION",
        "ON_HOLD",
        "READY_TO_SHIP",
        "DELIVERING"
    ];

    private readonly OpticalStoreDbContext _dbContext;

    // Khoi tao service dashboard voi db context.
    public DashboardService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private IQueryable<Order> RevenueOrders()
    {
        // Khớp cả "DELIVERED", "delivered", khoảng trắng thừa — tránh đơn cập nhật tay/SQL không khớp chuỗi tuyệt đối.
        return _dbContext.Orders.Where(o =>
            o.Status != null
            && (EF.Functions.ILike(o.Status.Trim(), StatusDelivered)
                || EF.Functions.ILike(o.Status.Trim(), StatusCompleted)));
    }

    // Tong hop chi so doanh thu va hoat dong cho dashboard.
    public async Task<DashboardRevenueDto> GetRevenueDashboardAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;

        // delivered_at / các cột timestamp without time zone: dùng Unspecified để tránh lỗi Npgsql với Kind=UTC.
        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var nextMonthStart = monthStart.AddMonths(1);
        var prevMonthStart = monthStart.AddMonths(-1);

        // Doanh thu: don da giao (DELIVERED) hoac customer da xac nhan (COMPLETED), cong total_amount.
        var revenue = await RevenueOrders()
            .SumAsync(o => o.TotalAmount ?? 0m, cancellationToken);

        var currentMonthRevenue = await RevenueOrders()
            .Where(o => o.DeliveredAt.HasValue && o.DeliveredAt.Value >= monthStart && o.DeliveredAt.Value < nextMonthStart)
            .SumAsync(o => o.TotalAmount ?? 0m, cancellationToken);

        var previousMonthRevenue = await RevenueOrders()
            .Where(o => o.DeliveredAt.HasValue && o.DeliveredAt.Value >= prevMonthStart && o.DeliveredAt.Value < monthStart)
            .SumAsync(o => o.TotalAmount ?? 0m, cancellationToken);

        var growth = previousMonthRevenue == 0m
            ? (currentMonthRevenue > 0m ? 100d : 0d)
            : (double)((currentMonthRevenue - previousMonthRevenue) / previousMonthRevenue * 100m);

        // Không dùng CreatedAt.Value.Date trong LINQ: với PostgreSQL timestamptz EF thường không dịch được → 500.
        var vietnamTimeZone = ResolveVietnamTimeZone();
        var nowVietnam = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, vietnamTimeZone);
        var todayStartVietnam = nowVietnam.Date;
        var tomorrowStartVietnam = todayStartVietnam.AddDays(1);
        var todayStartUtc = TimeZoneInfo.ConvertTimeToUtc(todayStartVietnam, vietnamTimeZone);
        var tomorrowStartUtc = TimeZoneInfo.ConvertTimeToUtc(tomorrowStartVietnam, vietnamTimeZone);

        var activeOrders = await _dbContext.Orders.LongCountAsync(x => x.Status != null && ActiveOrderStatuses.Contains(x.Status), cancellationToken);
        var ordersToday = await _dbContext.Orders.LongCountAsync(
            x => x.CreatedAt >= todayStartUtc && x.CreatedAt < tomorrowStartUtc,
            cancellationToken);
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

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsVietnamTimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(IanaVietnamTimeZoneId);
        }
    }
}
