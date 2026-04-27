using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Mappings;
using OpticalStore.API.Requests.Feedbacks;
using OpticalStore.API.Responses;
using OpticalStore.API.Swagger;
using OpticalStore.BLL.DTOs.Feedbacks;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("feedbacks")]
[Tags("14. Feedbacks")]
public sealed class FeedbackController : ControllerBase
{
    private readonly IFeedbackWorkflowService _feedbackWorkflowService;

    public FeedbackController(IFeedbackWorkflowService feedbackWorkflowService)
    {
        _feedbackWorkflowService = feedbackWorkflowService;
    }

    [HttpPost]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    [SwaggerMultipartJsonPart("feedback", typeof(FeedbackCreateRequest))]
    public async Task<ActionResult<ApiResponse<FeedbackResponseDto>>> Create([FromForm] string feedback, List<IFormFile>? images, CancellationToken cancellationToken)
    {
        var request = ParseJsonPayload<FeedbackCreateRequest>(feedback, "feedback");
        var userId = GetCurrentUserId();
        var uploadedImageNames = ToUploadedImageNames(images);
        var result = await _feedbackWorkflowService.CreateAsync(request.ToDto(), userId, User.IsInRole("ADMIN"), uploadedImageNames, cancellationToken);

        return Ok(new ApiResponse<FeedbackResponseDto>
        {
            Message = "Feedback submitted successfully!",
            Result = result
        });
    }

    [HttpPut("{feedbackId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    [SwaggerMultipartJsonPart("feedback", typeof(FeedbackUpdateRequest))]
    public async Task<ActionResult<ApiResponse<FeedbackResponseDto>>> Update(string feedbackId, [FromForm] string feedback, List<IFormFile>? images, CancellationToken cancellationToken)
    {
        var request = ParseJsonPayload<FeedbackUpdateRequest>(feedback, "feedback");
        var userId = GetCurrentUserId();
        var uploadedImageNames = ToUploadedImageNames(images);
        var result = await _feedbackWorkflowService.UpdateAsync(feedbackId, request.ToDto(), userId, User.IsInRole("ADMIN"), uploadedImageNames, cancellationToken);
        return Ok(new ApiResponse<FeedbackResponseDto> { Result = result });
    }

    [HttpDelete("{feedbackId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string feedbackId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        await _feedbackWorkflowService.DeleteAsync(feedbackId, userId, User.IsInRole("ADMIN"), cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Message = "Feedback deleted successfully!",
            Result = null
        });
    }

    [HttpGet("product/{productId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<FeedbackResponseDto>>>> GetByProduct(string productId, CancellationToken cancellationToken)
    {
        var result = await _feedbackWorkflowService.GetByProductAsync(productId, cancellationToken);

        return Ok(new ApiResponse<List<FeedbackResponseDto>> { Result = result });
    }

    [HttpGet("me")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<FeedbackResponseDto>>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _feedbackWorkflowService.GetMineAsync(userId, cancellationToken);

        return Ok(new ApiResponse<List<FeedbackResponseDto>> { Result = result });
    }

    [HttpGet("order/{orderId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<FeedbackResponseDto>>>> GetByOrder(string orderId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var result = await _feedbackWorkflowService.GetByOrderAsync(orderId, userId, User.IsInRole("ADMIN"), cancellationToken);

        return Ok(new ApiResponse<List<FeedbackResponseDto>> { Result = result });
    }

    [HttpGet("{feedbackId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<FeedbackResponseDto>>> GetById(string feedbackId, CancellationToken cancellationToken)
    {
        var result = await _feedbackWorkflowService.GetByIdAsync(feedbackId, cancellationToken);
        return Ok(new ApiResponse<FeedbackResponseDto> { Result = result });
    }

    private static T ParseJsonPayload<T>(string payload, string fieldName)
    {
        var parsed = JsonSerializer.Deserialize<T>(payload, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (parsed is null)
        {
            throw new AppException("INVALID_PAYLOAD", $"Invalid {fieldName} payload.", HttpStatusCode.BadRequest);
        }

        return parsed;
    }

    private static List<string>? ToUploadedImageNames(List<IFormFile>? images)
    {
        return images?.Select(x => $"uploaded://{x.FileName}").ToList();
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue("userId")
            ?? throw new AppException("UNAUTHENTICATED", "Missing userId claim.", HttpStatusCode.Unauthorized);
    }

}


