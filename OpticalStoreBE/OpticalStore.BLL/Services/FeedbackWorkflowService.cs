using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Feedbacks;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;

namespace OpticalStore.BLL.Services;

public sealed class FeedbackWorkflowService : IFeedbackWorkflowService
{
    private readonly OpticalStoreDbContext _dbContext;

    public FeedbackWorkflowService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<FeedbackResponseDto> CreateAsync(FeedbackCreateDto request, string userId, bool isAdmin, List<string>? uploadedImageNames, CancellationToken cancellationToken = default)
    {
        if (request.Rating < 1 || request.Rating > 5)
        {
            throw new AppException("INVALID_RATING", "Rating must be between 1 and 5.", HttpStatusCode.BadRequest);
        }

        var order = await _dbContext.Orders.Include(x => x.OrderItems).FirstOrDefaultAsync(x => x.Id == request.OrderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        if (order.CustomerId != userId && !isAdmin)
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

        if ((uploadedImageNames?.Count ?? 0) > 5)
        {
            throw new AppException("FEEDBACK_IMAGE_LIMIT_EXCEEDED", "Maximum 5 feedback images are allowed.", HttpStatusCode.BadRequest);
        }

        var entity = new DAL.Entities.Feedback
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

        return await MapFeedback(entity.Id, uploadedImageNames, cancellationToken);
    }

    public async Task<FeedbackResponseDto> UpdateAsync(string feedbackId, FeedbackUpdateDto request, string userId, bool isAdmin, List<string>? uploadedImageNames, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Feedbacks.FirstOrDefaultAsync(x => x.Id == feedbackId, cancellationToken);
        if (entity is null)
        {
            throw new AppException("FEEDBACK_NOT_FOUND", "Feedback not found.", HttpStatusCode.NotFound);
        }

        if (entity.CustomerId != userId && !isAdmin)
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

        return await MapFeedback(entity.Id, uploadedImageNames, cancellationToken);
    }

    public async Task DeleteAsync(string feedbackId, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.Feedbacks.FirstOrDefaultAsync(x => x.Id == feedbackId, cancellationToken);
        if (entity is null)
        {
            throw new AppException("FEEDBACK_NOT_FOUND", "Feedback not found.", HttpStatusCode.NotFound);
        }

        if (entity.CustomerId != userId && !isAdmin)
        {
            throw new AppException("FORBIDDEN", "You cannot delete this feedback.", HttpStatusCode.Forbidden);
        }

        _dbContext.Feedbacks.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<List<FeedbackResponseDto>> GetByProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        var ids = await _dbContext.Feedbacks.Where(x => x.ProductId == productId).OrderByDescending(x => x.CreatedAt).Select(x => x.Id).ToListAsync(cancellationToken);
        var result = new List<FeedbackResponseDto>();
        foreach (var id in ids)
        {
            result.Add(await MapFeedback(id, null, cancellationToken));
        }

        return result;
    }

    public async Task<List<FeedbackResponseDto>> GetMineAsync(string userId, CancellationToken cancellationToken = default)
    {
        var ids = await _dbContext.Feedbacks.Where(x => x.CustomerId == userId).OrderByDescending(x => x.CreatedAt).Select(x => x.Id).ToListAsync(cancellationToken);
        var result = new List<FeedbackResponseDto>();
        foreach (var id in ids)
        {
            result.Add(await MapFeedback(id, null, cancellationToken));
        }

        return result;
    }

    public async Task<List<FeedbackResponseDto>> GetByOrderAsync(string orderId, string userId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders.FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken);
        if (order is null)
        {
            throw new AppException("ORDER_NOT_FOUND", "Order not found.", HttpStatusCode.NotFound);
        }

        if (order.CustomerId != userId && !isAdmin)
        {
            throw new AppException("FORBIDDEN", "You cannot access feedback for this order.", HttpStatusCode.Forbidden);
        }

        var ids = await _dbContext.Feedbacks.Where(x => x.OrderId == orderId).OrderByDescending(x => x.CreatedAt).Select(x => x.Id).ToListAsync(cancellationToken);
        var result = new List<FeedbackResponseDto>();
        foreach (var id in ids)
        {
            result.Add(await MapFeedback(id, null, cancellationToken));
        }

        return result;
    }

    public async Task<FeedbackResponseDto> GetByIdAsync(string feedbackId, CancellationToken cancellationToken = default)
    {
        return await MapFeedback(feedbackId, null, cancellationToken);
    }

    private async Task<FeedbackResponseDto> MapFeedback(string feedbackId, List<string>? uploadedImageNames, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Feedbacks
            .Include(x => x.Customer)
            .Include(x => x.Product)
            .FirstOrDefaultAsync(x => x.Id == feedbackId, cancellationToken);

        if (entity is null)
        {
            throw new AppException("FEEDBACK_NOT_FOUND", "Feedback not found.", HttpStatusCode.NotFound);
        }

        return new FeedbackResponseDto
        {
            FeedbackId = entity.Id,
            OrderId = entity.OrderId,
            ProductId = entity.ProductId,
            ProductName = entity.Product?.Name ?? string.Empty,
            CustomerId = entity.CustomerId,
            CustomerName = entity.Customer?.Username ?? string.Empty,
            Rating = entity.Rating,
            Comment = entity.Comment,
            ImageUrls = uploadedImageNames ?? new List<string>(),
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
