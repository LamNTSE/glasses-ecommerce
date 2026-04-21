using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpticalStore.BLL.Configuration;
using OpticalStore.BLL.DTOs.Payments;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class PaymentWorkflowService : IPaymentWorkflowService
{
    private const string PaymentMethodVnPay = "VNPAY";
    private const string PaymentStatusUnpaid = "UNPAID";
    private const string PaymentStatusPaid = "PAID";
    private const string PaymentStatusFailed = "FAILED";
    private const string PaymentPurposeDeposit = "DEPOSIT";
    private const string PaymentPurposeRemaining = "REMAINING";
    private const string PaymentPurposeFull = "FULL";

    private readonly OpticalStoreDbContext _dbContext;
    private readonly VnpayOptions _vnpayOptions;

    public PaymentWorkflowService(OpticalStoreDbContext dbContext, IOptions<VnpayOptions> vnpayOptions)
    {
        _dbContext = dbContext;
        _vnpayOptions = vnpayOptions.Value;
    }

    public async Task<PaymentRequirementResultDto> GetPaymentRequirementAsync(PaymentRequirementDto request, CancellationToken cancellationToken = default)
    {
        var itemRequirements = new List<PaymentRequirementItemResultDto>();
        decimal orderTotal = 0m;
        decimal requiredPaymentTotal = 0m;

        foreach (var item in request.Items)
        {
            if (item.Quantity < 1)
            {
                throw new AppException("INVALID_QUANTITY", "Quantity must be >= 1.", HttpStatusCode.BadRequest);
            }

            var variant = item.ProductVariantId is null
                ? null
                : await _dbContext.ProductVariants.FirstOrDefaultAsync(x => x.Id == item.ProductVariantId && !(x.IsDeleted ?? false), cancellationToken);
            var lens = item.LensId is null
                ? null
                : await _dbContext.Lens.FirstOrDefaultAsync(x => x.Id == item.LensId && !x.IsDeleted, cancellationToken);

            var unitPrice = variant?.Price ?? 0m;
            var lensPrice = lens?.Price ?? 0m;
            var baseItemTotal = unitPrice * item.Quantity;
            var lensPriceTotal = lensPrice * item.Quantity;
            var itemTotal = baseItemTotal + lensPriceTotal;

            var paymentPercentage = string.Equals(variant?.OrderItemType, "PRE_ORDER", StringComparison.OrdinalIgnoreCase) ? 0.5m : 1m;
            var requiredPayment = baseItemTotal * paymentPercentage + lensPriceTotal;

            orderTotal += itemTotal;
            requiredPaymentTotal += requiredPayment;

            itemRequirements.Add(new PaymentRequirementItemResultDto
            {
                OrderItemId = null,
                OrderItemType = variant?.OrderItemType ?? "IN_STOCK",
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                LensPrice = lensPrice,
                LensPriceTotal = lensPriceTotal,
                BaseItemTotal = baseItemTotal,
                ItemTotal = itemTotal,
                PaymentPercentage = paymentPercentage,
                RequiredPayment = requiredPayment
            });
        }

        var remaining = Math.Max(0m, orderTotal - requiredPaymentTotal);

        return new PaymentRequirementResultDto
        {
            DepositPercentage = 0.5m,
            RequiredAmount = requiredPaymentTotal,
            OrderTotal = orderTotal,
            RequiredPaymentTotal = requiredPaymentTotal,
            RemainingPaymentTotal = remaining,
            ItemRequirements = itemRequirements,
            AllowCod = requiredPaymentTotal == 0,
            Message = requiredPaymentTotal == orderTotal
                ? "Full payment is required for in-stock items."
                : "Deposit is required for pre-order items."
        };
    }

    public async Task<string> CheckoutAsync(string orderId, string? clientIpAddress = null, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(x => x.Payments)
            .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);

        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        var hasPaidDeposit = order.Payments.Any(x => string.Equals(x.PaymentPurpose, PaymentPurposeDeposit, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Status, PaymentStatusPaid, StringComparison.OrdinalIgnoreCase));
        var purpose = (order.RemainingAmount ?? 0m) <= 0m
            ? PaymentPurposeFull
            : hasPaidDeposit ? PaymentPurposeRemaining : PaymentPurposeDeposit;

        var amount = purpose switch
        {
            PaymentPurposeFull => order.TotalAmount ?? 0m,
            PaymentPurposeDeposit => order.DepositAmount ?? 0m,
            _ => order.RemainingAmount ?? 0m
        };

        if (amount <= 0m)
        {
            throw new AppException("INVALID_PAYMENT_AMOUNT", "Payment amount must be greater than zero.", HttpStatusCode.BadRequest);
        }

        var payment = new Payment
        {
            Id = Guid.NewGuid().ToString(),
            OrderId = order.Id,
            PaymentMethod = PaymentMethodVnPay,
            PaymentPurpose = purpose,
            Amount = decimal.Round(amount, 0, MidpointRounding.AwayFromZero),
            Status = PaymentStatusUnpaid,
            Description = $"VNPAY payment for order {order.Id} ({purpose})"
        };

        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return BuildPaymentUrl(payment, clientIpAddress);
    }

    public Task<VnPayProcessResultDto> HandleVnPayReturnAsync(IReadOnlyDictionary<string, string> query, CancellationToken cancellationToken = default)
    {
        return ProcessVnPayCallbackAsync(query, true, cancellationToken);
    }

    public Task<VnPayProcessResultDto> HandleVnPayIpnAsync(IReadOnlyDictionary<string, string> query, CancellationToken cancellationToken = default)
    {
        return ProcessVnPayCallbackAsync(query, false, cancellationToken);
    }

    public async Task<List<PaymentHistoryItemDto>> GetPaymentHistoryAsync(string orderId, CancellationToken cancellationToken = default)
    {
        var data = await _dbContext.Payments
            .Include(x => x.Transactions)
            .Where(x => x.OrderId == orderId)
            .OrderByDescending(x => x.PaymentDate)
            .ToListAsync(cancellationToken);

        return data.Select(x => new PaymentHistoryItemDto
        {
            Id = x.Id,
            PaymentMethod = x.PaymentMethod,
            PaymentPurpose = x.PaymentPurpose,
            Amount = x.Amount,
            Percentage = x.Percentage,
            Status = x.Status,
            PaymentDate = x.PaymentDate,
            Description = x.Description,
            TransactionReference = x.Transactions.FirstOrDefault()?.GatewayReference
        }).ToList();
    }

    private async Task<VnPayProcessResultDto> ProcessVnPayCallbackAsync(IReadOnlyDictionary<string, string> query, bool isBrowserReturn, CancellationToken cancellationToken)
    {
        if (query is null || query.Count == 0)
        {
            return BuildCallbackResult(isBrowserReturn, false, "99", "Input data required", null, null);
        }

        var responseData = ExtractVnPayData(query);
        if (!responseData.TryGetValue("vnp_SecureHash", out var secureHash) || string.IsNullOrWhiteSpace(secureHash))
        {
            return BuildCallbackResult(isBrowserReturn, false, "99", "Input data required", null, null);
        }

        if (!ValidateSignature(responseData, secureHash))
        {
            return BuildCallbackResult(isBrowserReturn, false, "97", "Invalid signature", null, null);
        }

        if (!responseData.TryGetValue("vnp_TxnRef", out var paymentId) || string.IsNullOrWhiteSpace(paymentId))
        {
            return BuildCallbackResult(isBrowserReturn, false, "01", "Order not found", null, null);
        }

        var payment = await _dbContext.Payments
            .Include(x => x.Order)
            .Include(x => x.Transactions)
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
        {
            return BuildCallbackResult(isBrowserReturn, false, "01", "Order not found", null, null);
        }

        if (!TryReadAmount(responseData, "vnp_Amount", out var responseAmount))
        {
            return BuildCallbackResult(isBrowserReturn, false, "04", "invalid amount", payment.OrderId, payment.Id);
        }

        var expectedAmount = decimal.Round(payment.Amount ?? 0m, 0, MidpointRounding.AwayFromZero);
        if (responseAmount != expectedAmount)
        {
            return BuildCallbackResult(isBrowserReturn, false, "04", "invalid amount", payment.OrderId, payment.Id);
        }

        var responseCode = ReadQueryValue(responseData, "vnp_ResponseCode");
        var transactionStatus = ReadQueryValue(responseData, "vnp_TransactionStatus");
        var transactionNo = ReadQueryValue(responseData, "vnp_TransactionNo");
        var bankCode = ReadQueryValue(responseData, "vnp_BankCode");
        var payDate = ReadQueryValue(responseData, "vnp_PayDate");
        var isSuccess = responseCode == "00" && transactionStatus == "00";

        if (string.Equals(payment.Status, PaymentStatusPaid, StringComparison.OrdinalIgnoreCase))
        {
            return BuildCallbackResult(isBrowserReturn, true, "02", "Order already confirmed", payment.OrderId, payment.Id);
        }

        payment.PaymentDate = payment.PaymentDate ?? ToDatabaseDateTime(DateTime.UtcNow);
        payment.Status = isSuccess ? PaymentStatusPaid : PaymentStatusFailed;
        payment.Description = BuildPaymentDescription(payment, responseCode, transactionStatus, bankCode, payDate);

        if (!string.IsNullOrWhiteSpace(transactionNo) && !payment.Transactions.Any(x => string.Equals(x.GatewayReference, transactionNo, StringComparison.OrdinalIgnoreCase)))
        {
            _dbContext.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid().ToString(),
                PaymentId = payment.Id,
                Amount = payment.Amount,
                DateTime = ToDatabaseDateTime(DateTime.UtcNow),
                GatewayReference = transactionNo,
                Type = PaymentMethodVnPay,
                Description = BuildTransactionDescription(responseCode, transactionStatus, bankCode, payDate)
            });
        }

        if (isSuccess && payment.Order is not null)
        {
            ApplySuccessfulPaymentToOrder(payment);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var redirectUrl = BuildFrontendRedirectUrl(isSuccess ? "/checkout/success" : "/checkout/failure", payment.OrderId, payment.Id, responseCode, transactionStatus);
        return BuildCallbackResult(isBrowserReturn, isSuccess, "00", "Confirm Success", payment.OrderId, payment.Id, redirectUrl);
    }

    private void ApplySuccessfulPaymentToOrder(Payment payment)
    {
        if (payment.Order is null)
        {
            return;
        }

        if (string.Equals(payment.PaymentPurpose, PaymentPurposeDeposit, StringComparison.OrdinalIgnoreCase))
        {
            payment.Order.PreOrderStatus = "DEPOSIT_PAID";
            payment.Order.Status = "AWAITING_VERIFICATION";
            return;
        }

        if (string.Equals(payment.PaymentPurpose, PaymentPurposeRemaining, StringComparison.OrdinalIgnoreCase))
        {
            payment.Order.PreOrderStatus = "REMAINING_PAID";
            payment.Order.Status = "PREPARING";
            return;
        }

        payment.Order.Status = "PREPARING";
    }

    private string BuildPaymentUrl(Payment payment, string? clientIpAddress)
    {
        var createDate = DateTime.UtcNow.AddHours(7);
        var expireDate = createDate.AddMinutes(Math.Max(1, _vnpayOptions.ExpireMinutes));

        var requestData = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = ((long)decimal.Round(payment.Amount ?? 0m, 0, MidpointRounding.AwayFromZero) * 100L).ToString(CultureInfo.InvariantCulture),
            ["vnp_Command"] = "pay",
            ["vnp_CreateDate"] = createDate.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = _vnpayOptions.CurrencyCode,
            ["vnp_ExpireDate"] = expireDate.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_IpAddr"] = NormalizeIpAddress(clientIpAddress),
            ["vnp_Locale"] = _vnpayOptions.Locale,
            ["vnp_OrderInfo"] = $"Thanh toan don hang: {payment.OrderId}",
            ["vnp_OrderType"] = _vnpayOptions.OrderType,
            ["vnp_ReturnUrl"] = _vnpayOptions.ReturnUrl,
            ["vnp_TmnCode"] = _vnpayOptions.TmnCode,
            ["vnp_TxnRef"] = payment.Id,
            ["vnp_Version"] = _vnpayOptions.Version
        };

        var queryString = BuildQueryString(requestData);
        var secureHash = ComputeHmacSha512(_vnpayOptions.HashSecret, queryString);
        return string.Concat(_vnpayOptions.Url.TrimEnd('/'), "?", queryString, "&vnp_SecureHash=", secureHash);
    }

    private VnPayProcessResultDto BuildCallbackResult(bool isBrowserReturn, bool isSuccess, string rspCode, string message, string? orderId, string? paymentId, string? redirectUrl = null)
    {
        return new VnPayProcessResultDto
        {
            RspCode = rspCode,
            Message = message,
            IsSuccessful = isSuccess || rspCode == "02",
            RedirectUrl = isBrowserReturn
                ? redirectUrl ?? BuildFrontendRedirectUrl(isSuccess || rspCode == "02" ? "/checkout/success" : "/checkout/failure", orderId, paymentId, rspCode, null)
                : string.Empty
        };
    }

    private string BuildFrontendRedirectUrl(string path, string? orderId, string? paymentId, string? responseCode, string? transactionStatus)
    {
        var baseUrl = _vnpayOptions.FrontendBaseUrl.TrimEnd('/');
        var queryParameters = new List<string>();

        if (!string.IsNullOrWhiteSpace(orderId))
        {
            queryParameters.Add($"orderId={UrlEncode(orderId)}");
        }

        if (!string.IsNullOrWhiteSpace(paymentId))
        {
            queryParameters.Add($"paymentId={UrlEncode(paymentId)}");
        }

        if (!string.IsNullOrWhiteSpace(responseCode))
        {
            queryParameters.Add($"responseCode={UrlEncode(responseCode)}");
        }

        if (!string.IsNullOrWhiteSpace(transactionStatus))
        {
            queryParameters.Add($"transactionStatus={UrlEncode(transactionStatus)}");
        }

        return queryParameters.Count == 0
            ? baseUrl + path
            : baseUrl + path + "?" + string.Join("&", queryParameters);
    }

    private static string BuildQueryString(IEnumerable<KeyValuePair<string, string>> parameters)
    {
        return string.Join("&", parameters.Select(parameter => $"{UrlEncode(parameter.Key)}={UrlEncode(parameter.Value)}"));
    }

    private bool ValidateSignature(IReadOnlyDictionary<string, string> responseData, string expectedSignature)
    {
        var signData = responseData
            .Where(x => !string.Equals(x.Key, "vnp_SecureHash", StringComparison.OrdinalIgnoreCase) && !string.Equals(x.Key, "vnp_SecureHashType", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value ?? string.Empty, StringComparer.Ordinal);

        var queryString = BuildQueryString(signData.OrderBy(x => x.Key, StringComparer.Ordinal));
        var computedSignature = ComputeHmacSha512(_vnpayOptions.HashSecret, queryString);

        return string.Equals(computedSignature, expectedSignature, StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ExtractVnPayData(IReadOnlyDictionary<string, string> query)
    {
        return query
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Key.StartsWith("vnp_", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }

    private static bool TryReadAmount(IReadOnlyDictionary<string, string> data, string key, out decimal amount)
    {
        amount = 0m;

        if (!TryGetValue(data, key, out var rawAmount) || string.IsNullOrWhiteSpace(rawAmount))
        {
            return false;
        }

        if (!long.TryParse(rawAmount, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amountInMinorUnit))
        {
            return false;
        }

        amount = amountInMinorUnit / 100m;
        return true;
    }

    private static string ReadQueryValue(IReadOnlyDictionary<string, string> data, string key)
    {
        return TryGetValue(data, key, out var value) ? value ?? string.Empty : string.Empty;
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, string> data, string key, out string? value)
    {
        if (data.TryGetValue(key, out value))
        {
            return true;
        }

        var matched = data.FirstOrDefault(item => string.Equals(item.Key, key, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(matched.Key))
        {
            value = matched.Value;
            return true;
        }

        value = null;
        return false;
    }

    private static string ComputeHmacSha512(string key, string input)
    {
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(key));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static DateTime ToDatabaseDateTime(DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
    }

    private static string BuildPaymentDescription(Payment payment, string responseCode, string transactionStatus, string? bankCode, string? payDate)
    {
        var descriptionParts = new List<string>
        {
            $"paymentId={payment.Id}",
            $"responseCode={responseCode}",
            $"transactionStatus={transactionStatus}"
        };

        if (!string.IsNullOrWhiteSpace(bankCode))
        {
            descriptionParts.Add($"bankCode={bankCode}");
        }

        if (!string.IsNullOrWhiteSpace(payDate))
        {
            descriptionParts.Add($"payDate={payDate}");
        }

        return string.Join("; ", descriptionParts);
    }

    private static string BuildTransactionDescription(string responseCode, string transactionStatus, string? bankCode, string? payDate)
    {
        var descriptionParts = new List<string>
        {
            $"responseCode={responseCode}",
            $"transactionStatus={transactionStatus}"
        };

        if (!string.IsNullOrWhiteSpace(bankCode))
        {
            descriptionParts.Add($"bankCode={bankCode}");
        }

        if (!string.IsNullOrWhiteSpace(payDate))
        {
            descriptionParts.Add($"payDate={payDate}");
        }

        return string.Join("; ", descriptionParts);
    }

    private static string NormalizeIpAddress(string? clientIpAddress)
    {
        if (string.IsNullOrWhiteSpace(clientIpAddress))
        {
            return "127.0.0.1";
        }

        if (IPAddress.TryParse(clientIpAddress, out var parsedIpAddress))
        {
            if (parsedIpAddress.Equals(IPAddress.Loopback))
            {
                return "127.0.0.1";
            }

            if (parsedIpAddress.IsIPv4MappedToIPv6)
            {
                return parsedIpAddress.MapToIPv4().ToString();
            }

            return parsedIpAddress.ToString();
        }

        return clientIpAddress;
    }

    private static string UrlEncode(string value)
    {
        return WebUtility.UrlEncode(value) ?? string.Empty;
    }
}
