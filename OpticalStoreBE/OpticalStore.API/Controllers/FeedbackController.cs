using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpticalStore.API.Responses;
using OpticalStore.BLL.Exceptions;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("feedbacks")]
[Tags("15. Feedbacks")]
public sealed class FeedbackController : ControllerBase
{
    private readonly OpticalStoreDbContext _dbContext;

    public FeedbackController(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromForm] string feedback, List<IFormFile>? images, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<FeedbackCreateRequest>(feedback, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new AppException("INVALID_PAYLOAD", "Invalid feedback payload.", HttpStatusCode.BadRequest);

        if (request.Rating < 1 || request.Rating > 5)
        {
            throw new AppException("INVALID_RATING", "Rating must be between 1 and 5.", HttpStatusCode.BadRequest);
        }

        var userId = GetCurrentUserId();
        var order = await _dbContext.Orders.Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        if (order.CustomerId != userId && !User.IsInRole("ADMIN"))
        {
            throw new AppException("FORBIDDEN", "You cannot submit feedback for this order.", HttpStatusCode.Forbidden);
        }

        if (order.Status != "COMPLETED")
        {
            throw new AppException("FEEDBACK_ORDER_NOT_COMPLETED", "Only completed orders can be reviewed.", HttpStatusCode.BadRequest);
        }

        var productInOrder = await _dbContext.OrderItems
            .Include(x => x.ProductVariant)
            .AnyAsync(x => x.OrderId == order.Id && x.ProductVariant != null && x.ProductVariant.ProductId == request.ProductId, cancellationToken);

        if (!productInOrder)
        {
            throw new AppException("FEEDBACK_PRODUCT_NOT_IN_ORDER", "Product is not in this order.", HttpStatusCode.BadRequest);
        }

        var existed = await _dbContext.Feedbacks.AnyAsync(
            x => x.OrderId == request.OrderId && x.ProductId == request.ProductId && x.CustomerId == userId,
            cancellationToken);

        if (existed)
        {
            throw new AppException("FEEDBACK_ALREADY_EXISTS", "Feedback already exists for this order/product.", HttpStatusCode.BadRequest);
        }

        if ((images?.Count ?? 0) > 5)
        {
            throw new AppException("FEEDBACK_IMAGE_LIMIT_EXCEEDED", "Maximum 5 feedback images are allowed.", HttpStatusCode.BadRequest);
        }

        var entity = new Feedback
        {
            Id = Guid.NewGuid().ToString(),
            OrderId = request.OrderId,
            ProductId = request.ProductId,
            CustomerId = userId,
            Rating = request.Rating,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Feedbacks.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = await MapFeedback(entity.Id, images, cancellationToken);
        return Ok(new ApiResponse<object>
        {
            Message = "Feedback submitted successfully!",
            Result = result
        });
    }

    [HttpPut("{feedbackId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Update(string feedbackId, [FromForm] string feedback, List<IFormFile>? images, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<FeedbackUpdateRequest>(feedback, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new AppException("INVALID_PAYLOAD", "Invalid feedback payload.", HttpStatusCode.BadRequest);

        var entity = await _dbContext.Feedbacks.FirstOrDefaultAsync(x => x.Id == feedbackId, cancellationToken);
        if (entity is null)
        {
            throw new AppException("FEEDBACK_NOT_FOUND", "Feedback not found.", HttpStatusCode.NotFound);
        }

        var userId = GetCurrentUserId();
        if (entity.CustomerId != userId && !User.IsInRole("ADMIN"))
        {
            throw new AppException("FORBIDDEN", "You cannot update this feedback.", HttpStatusCode.Forbidden);
        }

        if (request.Rating.HasValue)
        {
            if (request.Rating.Value < 1 || request.Rating.Value > 5)
            {
                throw new AppException("INVALID_RATING", "Rating must be between 1 and 5.", HttpStatusCode.BadRequest);
            }

            entity.Rating = request.Rating.Value;
        }

        if (request.Comment is not null)
        {
            entity.Comment = request.Comment;
        }

        entity.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = await MapFeedback(entity.Id, images, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpDelete("{feedbackId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string feedbackId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Feedbacks.FirstOrDefaultAsync(x => x.Id == feedbackId, cancellationToken);
        if (entity is null)
        {
            throw new AppException("FEEDBACK_NOT_FOUND", "Feedback not found.", HttpStatusCode.NotFound);
        }

        var userId = GetCurrentUserId();
        if (entity.CustomerId != userId && !User.IsInRole("ADMIN"))
        {
            throw new AppException("FORBIDDEN", "You cannot delete this feedback.", HttpStatusCode.Forbidden);
        }

        _dbContext.Feedbacks.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Message = "Feedback deleted successfully!",
            Result = null
        });
    }

    [HttpGet("product/{productId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetByProduct(string productId, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.Feedbacks.Where(x => x.ProductId == productId).OrderByDescending(x => x.CreatedAt).Select(x => x.Id).ToListAsync(cancellationToken);
        var result = new List<object>();
        foreach (var id in ids)
        {
            result.Add(await MapFeedback(id, null, cancellationToken));
        }

        return Ok(new ApiResponse<List<object>> { Result = result });
    }

    [HttpGet("me")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetMine(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var ids = await _dbContext.Feedbacks.Where(x => x.CustomerId == userId).OrderByDescending(x => x.CreatedAt).Select(x => x.Id).ToListAsync(cancellationToken);
        var result = new List<object>();
        foreach (var id in ids)
        {
            result.Add(await MapFeedback(id, null, cancellationToken));
        }

        return Ok(new ApiResponse<List<object>> { Result = result });
    }

    [HttpGet("order/{orderId}")]
    [Authorize(Roles = "CUSTOMER,ADMIN")]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetByOrder(string orderId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        if (order.CustomerId != userId && !User.IsInRole("ADMIN"))
        {
            throw new AppException("FORBIDDEN", "You cannot access feedback for this order.", HttpStatusCode.Forbidden);
        }

        var ids = await _dbContext.Feedbacks.Where(x => x.OrderId == orderId).OrderByDescending(x => x.CreatedAt).Select(x => x.Id).ToListAsync(cancellationToken);
        var result = new List<object>();
        foreach (var id in ids)
        {
            result.Add(await MapFeedback(id, null, cancellationToken));
        }

        return Ok(new ApiResponse<List<object>> { Result = result });
    }

    [HttpGet("{feedbackId}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<object>>> GetById(string feedbackId, CancellationToken cancellationToken)
    {
        var result = await MapFeedback(feedbackId, null, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    private async Task<object> MapFeedback(string feedbackId, List<IFormFile>? uploadedImages, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Feedbacks
            .Include(x => x.Customer)
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == feedbackId, cancellationToken);

        if (entity is null)
        {
            throw new AppException("FEEDBACK_NOT_FOUND", "Feedback not found.", HttpStatusCode.NotFound);
        }

        var imageUrls = uploadedImages?.Select(x => $"uploaded://{x.FileName}").ToList() ?? new List<string>();

        return new
        {
            feedbackId = entity.Id,
            orderId = entity.OrderId,
            productId = entity.ProductId,
            productName = entity.Product.Name,
            customerId = entity.CustomerId,
            customerName = entity.Customer.Username,
            rating = entity.Rating,
            comment = entity.Comment,
            imageUrls,
            createdAt = entity.CreatedAt,
            updatedAt = entity.UpdatedAt
        };
    }

    private string GetCurrentUserId()
    {
        return User.FindFirstValue("userId")
            ?? throw new AppException("UNAUTHENTICATED", "Missing userId claim.", HttpStatusCode.Unauthorized);
    }

    public sealed class FeedbackCreateRequest
    {
        public string OrderId { get; set; } = string.Empty;

        public string ProductId { get; set; } = string.Empty;

        public int Rating { get; set; }

        public string? Comment { get; set; }
    }

    public sealed class FeedbackUpdateRequest
    {
        public int? Rating { get; set; }

        public string? Comment { get; set; }
    }
}


