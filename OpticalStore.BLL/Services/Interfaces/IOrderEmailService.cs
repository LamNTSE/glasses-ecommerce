using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services.Interfaces;

public interface IOrderEmailService
{
    Task SendOrderCreatedEmailAsync(Order order, User customer, IEnumerable<OrderItem> orderItems, CancellationToken cancellationToken = default);

    Task SendOrderConfirmationEmailAsync(Order order, User customer, IEnumerable<OrderItem> orderItems, CancellationToken cancellationToken = default);

    Task SendOrderDeliveredEmailAsync(Order order, User customer, IEnumerable<OrderItem> orderItems, CancellationToken cancellationToken = default);

    Task SendOrderCancelledEmailAsync(Order order, User customer, IEnumerable<OrderItem> orderItems, string? cancellationReason, CancellationToken cancellationToken = default);
}
