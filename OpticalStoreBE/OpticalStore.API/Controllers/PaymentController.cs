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

    public PaymentController(IPaymentWorkflowService paymentWorkflowService)
    {
        _paymentWorkflowService = paymentWorkflowService;
    }

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

    [HttpPost("checkout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<string>>> Checkout([FromQuery] string orderId, CancellationToken cancellationToken)
    {
        var paymentUrl = await _paymentWorkflowService.CheckoutAsync(orderId, cancellationToken);
        return Ok(new ApiResponse<string> { Result = paymentUrl });
    }

    [HttpGet("vnpay-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> VnPayCallback(CancellationToken cancellationToken)
    {
        var paymentId = Request.Query["vnp_TxnRef"].ToString();
        var transactionStatus = Request.Query["vnp_TransactionStatus"].ToString();

        var redirectPath = await _paymentWorkflowService.HandleVnPayCallbackAsync(paymentId, transactionStatus, cancellationToken);
        return Redirect(redirectPath);
    }

    [HttpGet("orders/{orderId}/history")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<PaymentHistoryItemDto>>>> GetPaymentHistory(string orderId, CancellationToken cancellationToken)
    {
        var result = await _paymentWorkflowService.GetPaymentHistoryAsync(orderId, cancellationToken);

        return Ok(new ApiResponse<List<PaymentHistoryItemDto>> { Result = result });
    }

}
