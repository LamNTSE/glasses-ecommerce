using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpticalStore.DAL.DBContext;

namespace OpticalStore.BLL.Services;

// Chạy nền: cứ 5 phút quét một lần, những đơn DELIVERED đã quá 3600s thì tự set COMPLETED.
public class AutoCompleteOrderService(IServiceScopeFactory scopeFactory, ILogger<AutoCompleteOrderService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CompletionDelay = TimeSpan.FromSeconds(3600);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, stoppingToken);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<OpticalStoreDbContext>();

                var cutoff = DateTime.UtcNow - CompletionDelay;

                var orders = await db.Orders
                    .Where(o => o.Status == "DELIVERED" && o.DeliveredAt.HasValue && o.DeliveredAt.Value <= cutoff)
                    .ToListAsync(stoppingToken);

                if (orders.Count == 0) continue;

                foreach (var order in orders)
                    order.Status = "COMPLETED";

                await db.SaveChangesAsync(stoppingToken);
                logger.LogInformation("AutoComplete: {Count} đơn DELIVERED → COMPLETED.", orders.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "AutoCompleteOrderService lỗi khi xử lý.");
            }
        }
    }
}
