using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Mappings;
using OpticalStore.API.Requests.Refunds;
using OpticalStore.API.Responses;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("refund")]
[Tags("11. Refund")]
public sealed class RefundController : ControllerBase
{
    private readonly IRefundWorkflowService _refundWorkflowService;

    public RefundController(IRefundWorkflowService refundWorkflowService)
    {
        _refundWorkflowService = refundWorkflowService;
    }

    [HttpPatch("variant/{variantId}/in-activate")]
    [Authorize(Roles = "MANAGER,ADMIN,CUSTOMER")]
    public async Task<ActionResult<ApiResponse<object>>> InactivateVariant(string variantId, CancellationToken cancellationToken)
    {
        var result = await _refundWorkflowService.InactivateVariantAsync(variantId, cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Result = result
        });
    }

    [HttpGet("affected-orders/{variantId}")]
    [Authorize(Roles = "MANAGER,ADMIN,CUSTOMER")]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetAffectedOrders(string variantId, CancellationToken cancellationToken)
    {
        var result = await _refundWorkflowService.GetAffectedOrdersAsync(variantId, cancellationToken);

        return Ok(new ApiResponse<List<object>> { Result = result });
    }

    [HttpPost("create-batch")]
    [Authorize(Roles = "MANAGER,ADMIN,CUSTOMER")]
    public async Task<ActionResult<ApiResponse<List<object>>>> CreateBatch([FromBody] RefundBatchRequest request, CancellationToken cancellationToken)
    {
        var results = await _refundWorkflowService.CreateBatchAsync(request.ToDto(), cancellationToken);
        return Ok(new ApiResponse<List<object>> { Result = results });
    }

    [HttpGet("ready")]
    [Authorize(Roles = "MANAGER,ADMIN,CUSTOMER")]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetReady(CancellationToken cancellationToken)
    {
        var result = await _refundWorkflowService.GetReadyAsync(cancellationToken);

        return Ok(new ApiResponse<List<object>> { Result = result });
    }

    [HttpPost("{refundId}/refund-checkout")]
    [Authorize(Roles = "MANAGER,ADMIN,CUSTOMER")]
    public async Task<ActionResult<ApiResponse<string>>> RefundCheckout(string refundId, CancellationToken cancellationToken)
    {
        var paymentUrl = await _refundWorkflowService.RefundCheckoutAsync(refundId, cancellationToken);
        return Ok(new ApiResponse<string> { Result = paymentUrl });
    }
}
