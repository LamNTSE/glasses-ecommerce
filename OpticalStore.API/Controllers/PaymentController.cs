using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Mappings;
using OpticalStore.API.Requests.Payments;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Payments;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("payment")]
[Tags("10. Payment")]
public sealed class PaymentController : ControllerBase
{
    private readonly IPaymentWorkflowService _paymentWorkflowService;

    // Khoi tao controller va gan service xu ly thanh toan.
    public PaymentController(IPaymentWorkflowService paymentWorkflowService)
    {
        _paymentWorkflowService = paymentWorkflowService;
    }

    // Lay yeu cau thanh toan can thiet cho mot don.
    [HttpPost("orders/requirement")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PaymentRequirementResultDto>>> GetPaymentRequirement([FromBody] PaymentRequirementRequest request, CancellationToken cancellationToken)
    {
        var result = await _paymentWorkflowService.GetPaymentRequirementAsync(request.ToDto(), cancellationToken);

        return Ok(new ApiResponse<PaymentRequirementResultDto>
        {
            Result = result
        });
    }

    // Tao URL checkout VNPay cho don hang.
    [HttpPost("checkout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<string>>> Checkout([FromQuery] string orderId, CancellationToken cancellationToken)
    {
        var clientIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var paymentUrl = await _paymentWorkflowService.CheckoutAsync(orderId, clientIpAddress, cancellationToken);
        return Ok(new ApiResponse<string> { Result = paymentUrl });
    }

    // Xu ly callback tu trang tra ve VNPay.
    [HttpGet("vnpay-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayCallback(CancellationToken cancellationToken)
    {
        var query = Request.Query.ToDictionary(x => x.Key, x => (string)x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var result = await _paymentWorkflowService.HandleVnPayReturnAsync(query, cancellationToken);

        return Redirect(result.RedirectUrl);
    }

    // Xu ly IPN server-to-server tu VNPay.
    [HttpGet("vnpay-ipn")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayIpn(CancellationToken cancellationToken)
    {
        var query = Request.Query.ToDictionary(x => x.Key, x => (string)x.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        var result = await _paymentWorkflowService.HandleVnPayIpnAsync(query, cancellationToken);

        return Ok(new
        {
            RspCode = result.RspCode,
            Message = result.Message
        });
    }

    // Lay lich su giao dich cua don hang.
    [HttpGet("orders/{orderId}/history")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<PaymentHistoryItemDto>>>> GetPaymentHistory(string orderId, CancellationToken cancellationToken)
    {
        var result = await _paymentWorkflowService.GetPaymentHistoryAsync(orderId, cancellationToken);

        return Ok(new ApiResponse<List<PaymentHistoryItemDto>> { Result = result });
    }

}
