using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpticalStore.BLL.Configuration;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class OrderEmailService : IOrderEmailService
{
    private readonly EmailOptions _emailOptions;
    private readonly ILogger<OrderEmailService> _logger;

    public OrderEmailService(IOptions<EmailOptions> emailOptions, ILogger<OrderEmailService> logger)
    {
        _emailOptions = emailOptions.Value;
        _logger = logger;
    }

    public async Task SendOrderCreatedEmailAsync(Order order, User customer, IEnumerable<OrderItem> orderItems, CancellationToken cancellationToken = default)
    {
        await SendOrderConfirmationEmailAsync(order, customer, orderItems, cancellationToken);
    }

    public async Task SendOrderConfirmationEmailAsync(Order order, User customer, IEnumerable<OrderItem> orderItems, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (!CanSend(customer.Email))
        {
            return;
        }

        var orderTrackingUrl = BuildOrderTrackingUrl();
        var customerName = ResolveCustomerName(customer);
        var subject = $"Xác nhận đơn hàng {order.Id}";
        var body = BuildOrderCreatedBody(order, customer, orderItems, customerName, orderTrackingUrl);

        await SendEmailAsync(customer.Email!, subject, body, order.Id, "order confirmation");
    }

    public async Task SendOrderDeliveredEmailAsync(Order order, User customer, IEnumerable<OrderItem> orderItems, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (!CanSend(customer.Email))
        {
            return;
        }

        var orderDetailUrl = BuildOrderDetailUrl(order.Id);
        var customerName = ResolveCustomerName(customer);
        var subject = $"Đơn hàng {order.Id} đã giao thành công";
        var body = BuildOrderDeliveredBody(order, customer, orderItems, customerName, orderDetailUrl);

        await SendEmailAsync(customer.Email!, subject, body, order.Id, "order delivered");
    }

    public async Task SendOrderCancelledEmailAsync(Order order, User customer, IEnumerable<OrderItem> orderItems, string? cancellationReason, CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        if (!CanSend(customer.Email))
        {
            return;
        }

        var orderDetailUrl = BuildOrderDetailUrl(order.Id);
        var customerName = ResolveCustomerName(customer);
        var subject = $"Đơn hàng {order.Id} đã bị hủy";
        var body = BuildOrderCancelledBody(order, customer, orderItems, customerName, orderDetailUrl, cancellationReason);

        await SendEmailAsync(customer.Email!, subject, body, order.Id, "order cancelled");
    }

    private async Task SendEmailAsync(string customerEmail, string subject, string body, string orderId, string logAction)
    {
        try
        {
            using var client = new SmtpClient(_emailOptions.Host, _emailOptions.Port)
            {
                EnableSsl = _emailOptions.UseSsl,
                Credentials = new NetworkCredential(_emailOptions.Username, _emailOptions.Password)
            };

            using var message = new MailMessage
            {
                From = new MailAddress(_emailOptions.FromAddress, _emailOptions.FromName),
                Subject = subject,
                SubjectEncoding = Encoding.UTF8,
                Body = body,
                BodyEncoding = Encoding.UTF8,
                IsBodyHtml = true
            };
            message.To.Add(customerEmail.Trim());

            await client.SendMailAsync(message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send {EmailAction} email for order {OrderId}.", logAction, orderId);
        }
    }

    private bool CanSend(string? customerEmail)
    {
        return _emailOptions.Enabled
            && !string.IsNullOrWhiteSpace(customerEmail)
            && !string.IsNullOrWhiteSpace(_emailOptions.Host)
            && _emailOptions.Port > 0
            && !string.IsNullOrWhiteSpace(_emailOptions.Username)
            && !string.IsNullOrWhiteSpace(_emailOptions.Password)
            && !string.IsNullOrWhiteSpace(_emailOptions.FromAddress);
    }

    private string BuildOrderTrackingUrl()
    {
        var baseUrl = _emailOptions.FrontendBaseUrl.TrimEnd('/');
        var path = _emailOptions.OrderHistoryPath.StartsWith('/') ? _emailOptions.OrderHistoryPath : "/" + _emailOptions.OrderHistoryPath;
        return $"{baseUrl}{path}";
    }

    private string BuildOrderDetailUrl(string orderId)
    {
        var trackingUrl = BuildOrderTrackingUrl();
        var separator = trackingUrl.Contains('?') ? "&" : "?";
        return $"{trackingUrl}{separator}orderId={WebUtility.UrlEncode(orderId)}";
    }

    /// <summary>Nội dung chân mail: hướng dẫn liên hệ CSKH — dùng cho mọi mail gửi khách hàng.</summary>
    private static string CustomerSupportFooterHtml()
    {
        return "<p style='margin-top:16px;font-size:14px;color:#374151'>Nếu cần hỗ trợ thêm, vui lòng liên hệ bộ phận CSKH của Optical Store.</p>";
    }

    private static string ResolveCustomerName(User customer)
    {
        var fullName = string.Join(" ", new[] { customer.FirstName, customer.LastName }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim()));

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        if (!string.IsNullOrWhiteSpace(customer.Username))
        {
            return customer.Username.Trim();
        }

        return customer.Email?.Trim() ?? "Khách hàng";
    }

    private static string BuildOrderCreatedBody(Order order, User customer, IEnumerable<OrderItem> orderItems, string customerName, string orderTrackingUrl)
    {
        var orderDisplayName = BuildOrderDisplayName(order, orderItems);
        var rowsBuilder = new StringBuilder();
        foreach (var item in orderItems)
        {
            var productName = item.ProductVariant?.Product?.Name ?? "Sản phẩm";
            var variantName = item.ProductVariant is null
                ? "-"
                : $"{item.ProductVariant.ColorName}-{item.ProductVariant.SizeLabel}";
            var quantity = item.Quantity ?? 0;
            var unitPrice = (item.UnitPrice ?? 0m) + (item.LensPrice ?? 0m);
            var lineTotal = item.TotalPrice ?? unitPrice * quantity;
            var lensName = string.IsNullOrWhiteSpace(item.LensName) ? "Không có" : item.LensName;

            rowsBuilder.Append($@"
                <tr>
                    <td style='padding:8px;border:1px solid #e5e7eb'>{WebUtility.HtmlEncode(productName)}</td>
                    <td style='padding:8px;border:1px solid #e5e7eb'>{WebUtility.HtmlEncode(variantName)}</td>
                    <td style='padding:8px;border:1px solid #e5e7eb'>{WebUtility.HtmlEncode(lensName)}</td>
                    <td style='padding:8px;border:1px solid #e5e7eb;text-align:center'>{quantity}</td>
                    <td style='padding:8px;border:1px solid #e5e7eb;text-align:right'>{FormatMoney(unitPrice)}</td>
                    <td style='padding:8px;border:1px solid #e5e7eb;text-align:right'>{FormatMoney(lineTotal)}</td>
                </tr>");
        }

        var deliveryAddress = string.IsNullOrWhiteSpace(order.DeliveryAddress) ? "Chưa cập nhật" : order.DeliveryAddress;
        var recipientName = string.IsNullOrWhiteSpace(order.RecipientName) ? customerName : order.RecipientName;
        var phone = string.IsNullOrWhiteSpace(order.PhoneNumber) ? (customer.Phone ?? "Chưa cập nhật") : order.PhoneNumber;
        var paymentMethod = string.IsNullOrWhiteSpace(order.PaymentMethod) ? "Chưa xác định" : order.PaymentMethod;
        var totalAmount = order.TotalAmount ?? 0m;

        return $@"
<div style='font-family:Arial,sans-serif;color:#111827;line-height:1.5'>
    <h2 style='margin:0 0 12px'>Cảm ơn bạn đã đặt hàng tại Optical Store</h2>
    <p>Xin chào <strong>{WebUtility.HtmlEncode(customerName)}</strong>, đơn hàng của bạn đã được tạo thành công.</p>
    <p><strong>Mã đơn hàng:</strong> {WebUtility.HtmlEncode(order.Id)}</p>
    <p><strong>Tên đơn hàng:</strong> {WebUtility.HtmlEncode(orderDisplayName)}</p>

    <h3 style='margin:18px 0 8px'>Chi tiết đơn hàng</h3>
    <table style='border-collapse:collapse;width:100%;font-size:14px'>
        <thead>
            <tr style='background:#f9fafb'>
                <th style='padding:8px;border:1px solid #e5e7eb;text-align:left'>Sản phẩm</th>
                <th style='padding:8px;border:1px solid #e5e7eb;text-align:left'>Phân loại</th>
                <th style='padding:8px;border:1px solid #e5e7eb;text-align:left'>Tròng kính</th>
                <th style='padding:8px;border:1px solid #e5e7eb;text-align:center'>SL</th>
                <th style='padding:8px;border:1px solid #e5e7eb;text-align:right'>Đơn giá</th>
                <th style='padding:8px;border:1px solid #e5e7eb;text-align:right'>Thành tiền</th>
            </tr>
        </thead>
        <tbody>
            {rowsBuilder}
        </tbody>
    </table>

    <p style='margin:12px 0 0'><strong>Tổng tiền:</strong> {FormatMoney(totalAmount)}</p>
    <p><strong>Phương thức thanh toán:</strong> {WebUtility.HtmlEncode(paymentMethod)}</p>

    <h3 style='margin:18px 0 8px'>Thông tin khách hàng và giao hàng</h3>
    <p style='margin:4px 0'><strong>Tên người nhận:</strong> {WebUtility.HtmlEncode(recipientName)}</p>
    <p style='margin:4px 0'><strong>Email:</strong> {WebUtility.HtmlEncode(customer.Email ?? "Chưa cập nhật")}</p>
    <p style='margin:4px 0'><strong>Số điện thoại:</strong> {WebUtility.HtmlEncode(phone)}</p>
    <p style='margin:4px 0'><strong>Địa chỉ giao hàng:</strong> {WebUtility.HtmlEncode(deliveryAddress)}</p>

    {CustomerSupportFooterHtml()}

    <p style='margin-top:20px'>
        <a href='{WebUtility.HtmlEncode(orderTrackingUrl)}'
           style='display:inline-block;background:#2563eb;color:#fff;padding:10px 16px;text-decoration:none;border-radius:6px;font-weight:600'>
           Theo dõi đơn
        </a>
    </p>
</div>";
    }

    private static string BuildOrderDeliveredBody(Order order, User customer, IEnumerable<OrderItem> orderItems, string customerName, string orderDetailUrl)
    {
        var orderDisplayName = BuildOrderDisplayName(order, orderItems);
        var deliveredAt = order.DeliveredAt.HasValue
            ? order.DeliveredAt.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"))
            : DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("vi-VN"));
        var deliveryAddress = string.IsNullOrWhiteSpace(order.DeliveryAddress) ? "Chưa cập nhật" : order.DeliveryAddress;
        var recipientName = string.IsNullOrWhiteSpace(order.RecipientName) ? customerName : order.RecipientName;
        var phone = string.IsNullOrWhiteSpace(order.PhoneNumber) ? (customer.Phone ?? "Chưa cập nhật") : order.PhoneNumber;

        return $@"
<div style='font-family:Arial,sans-serif;color:#111827;line-height:1.5'>
    <h2 style='margin:0 0 12px'>Đơn hàng của bạn đã được giao thành công</h2>
    <p>Xin chào <strong>{WebUtility.HtmlEncode(customerName)}</strong>, Optical Store thông báo đơn hàng của bạn đã được giao thành công.</p>
    <p><strong>Mã đơn hàng:</strong> {WebUtility.HtmlEncode(order.Id)}</p>
    <p><strong>Tên đơn hàng:</strong> {WebUtility.HtmlEncode(orderDisplayName)}</p>
    <p><strong>Thời gian giao:</strong> {WebUtility.HtmlEncode(deliveredAt)}</p>

    <h3 style='margin:18px 0 8px'>Thông tin giao hàng</h3>
    <p style='margin:4px 0'><strong>Tên người nhận:</strong> {WebUtility.HtmlEncode(recipientName)}</p>
    <p style='margin:4px 0'><strong>Số điện thoại:</strong> {WebUtility.HtmlEncode(phone)}</p>
    <p style='margin:4px 0'><strong>Địa chỉ giao hàng:</strong> {WebUtility.HtmlEncode(deliveryAddress)}</p>

    {CustomerSupportFooterHtml()}

    <p style='margin-top:20px'>
        <a href='{WebUtility.HtmlEncode(orderDetailUrl)}'
           style='display:inline-block;background:#2563eb;color:#fff;padding:10px 16px;text-decoration:none;border-radius:6px;font-weight:600'>
           Xem đơn hàng
        </a>
    </p>
</div>";
    }

    private static string BuildOrderCancelledBody(Order order, User customer, IEnumerable<OrderItem> orderItems, string customerName, string orderDetailUrl, string? cancellationReason)
    {
        var reason = string.IsNullOrWhiteSpace(cancellationReason)
            ? "Không có thông tin lý do cụ thể."
            : cancellationReason.Trim();
        var orderDisplayName = BuildOrderDisplayName(order, orderItems);

        return $@"
<div style='font-family:Arial,sans-serif;color:#111827;line-height:1.5'>
    <h2 style='margin:0 0 12px'>Đơn hàng của bạn đã bị hủy</h2>
    <p>Xin chào <strong>{WebUtility.HtmlEncode(customerName)}</strong>, rất tiếc đơn hàng của bạn không thể tiếp tục xử lý.</p>
    <p><strong>Tên đơn hàng:</strong> {WebUtility.HtmlEncode(orderDisplayName)}</p>
    <p><strong>Mã đơn hàng:</strong> {WebUtility.HtmlEncode(order.Id)}</p>
    <p><strong>Email tài khoản:</strong> {WebUtility.HtmlEncode(customer.Email ?? "Chưa cập nhật")}</p>
    <p><strong>Lý do hủy:</strong> {WebUtility.HtmlEncode(reason)}</p>
    {CustomerSupportFooterHtml()}

    <p style='margin-top:20px'>
        <a href='{WebUtility.HtmlEncode(orderDetailUrl)}'
           style='display:inline-block;background:#2563eb;color:#fff;padding:10px 16px;text-decoration:none;border-radius:6px;font-weight:600'>
           Xem chi tiết đơn hàng
        </a>
    </p>
</div>";
    }

    private static string BuildOrderDisplayName(Order order, IEnumerable<OrderItem> orderItems)
    {
        var items = orderItems.ToList();
        var isPreOrder = items.Any(x =>
        {
            var type = x.OrderItemType?.Trim().ToUpperInvariant().Replace("-", "_");
            return type == "PRE_ORDER" || type == "PREORDER";
        });

        var firstItem = items.FirstOrDefault();
        var productName = firstItem?.ProductVariant?.Product?.Name;
        if (string.IsNullOrWhiteSpace(productName))
        {
            productName = "Sản phẩm";
        }

        var hasLens = items.Any(x =>
            !string.IsNullOrWhiteSpace(x.LensId)
            || !string.IsNullOrWhiteSpace(x.LensName)
            || !string.IsNullOrWhiteSpace(x.PrescriptionId)
            || x.Prescription is not null);

        var typeLabel = isPreOrder ? "PREORDER" : "ORDER";
        var shortCode = order.Id[..Math.Min(8, order.Id.Length)].ToUpperInvariant();
        var productLabel = hasLens ? $"{productName} + Lens" : productName;
        return $"{typeLabel} - {productLabel} - {shortCode}";
    }

    private static string FormatMoney(decimal value)
    {
        return string.Format(CultureInfo.GetCultureInfo("vi-VN"), "{0:#,##0} VND", value);
    }
}
