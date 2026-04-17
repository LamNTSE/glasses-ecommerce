using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpticalStore.API.Responses;
using OpticalStore.BLL.Exceptions;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("api/combos")]
[Tags("8. Combos")]
public sealed class ComboController : ControllerBase
{
    private readonly OpticalStoreDbContext _dbContext;

    public ComboController(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Create([FromBody] ComboUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateComboRequest(request);

        var combo = new Combo
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            IsManuallyDisabled = request.IsManuallyDisabled ?? false,
            Status = ResolveComboStatus(request.StartTime, request.EndTime, request.IsManuallyDisabled ?? false),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsDeleted = false
        };

        _dbContext.Combos.Add(combo);

        var items = await BuildComboItems(combo.Id, request.ComboItems, cancellationToken);
        _dbContext.ComboItems.AddRange(items);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = await BuildComboResponse(combo.Id, cancellationToken);
        return Ok(new ApiResponse<object> { Message = "Tạo combo thành công", Result = result });
    }

    [HttpPut("{comboId}")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Update(string comboId, [FromBody] ComboUpsertRequest request, CancellationToken cancellationToken)
    {
        ValidateComboRequest(request);

        var combo = await _dbContext.Combos.FirstOrDefaultAsync(x => x.Id == comboId && !(x.IsDeleted ?? false), cancellationToken);
        if (combo is null)
        {
            throw new AppException("COMBO_NOT_FOUND", "Combo not found.", HttpStatusCode.NotFound);
        }

        combo.Name = request.Name.Trim();
        combo.Description = request.Description?.Trim();
        combo.DiscountType = request.DiscountType;
        combo.DiscountValue = request.DiscountValue;
        combo.StartTime = request.StartTime;
        combo.EndTime = request.EndTime;
        combo.IsManuallyDisabled = request.IsManuallyDisabled ?? combo.IsManuallyDisabled;
        combo.Status = ResolveComboStatus(combo.StartTime, combo.EndTime, combo.IsManuallyDisabled);
        combo.UpdatedAt = DateTime.UtcNow;

        var oldItems = _dbContext.ComboItems.Where(x => x.ComboId == comboId);
        _dbContext.ComboItems.RemoveRange(oldItems);

        var newItems = await BuildComboItems(combo.Id, request.ComboItems, cancellationToken);
        _dbContext.ComboItems.AddRange(newItems);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = await BuildComboResponse(combo.Id, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpPatch("{comboId}/status")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateStatus(string comboId, [FromBody] ComboStatusRequest request, CancellationToken cancellationToken)
    {
        var combo = await _dbContext.Combos.FirstOrDefaultAsync(x => x.Id == comboId && !(x.IsDeleted ?? false), cancellationToken);
        if (combo is null)
        {
            throw new AppException("COMBO_NOT_FOUND", "Combo not found.", HttpStatusCode.NotFound);
        }

        if (!string.Equals(request.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(request.Status, "INACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException("INVALID_STATUS", "Status must be ACTIVE or INACTIVE.", HttpStatusCode.BadRequest);
        }

        if (string.Equals(request.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) && combo.EndTime < DateTime.UtcNow)
        {
            throw new AppException("COMBO_CANNOT_ACTIVATE_EXPIRED", "Cannot activate expired combo.", HttpStatusCode.BadRequest);
        }

        combo.Status = request.Status.ToUpperInvariant();
        combo.IsManuallyDisabled = combo.Status == "INACTIVE";
        combo.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        var result = await BuildComboResponse(combo.Id, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpGet]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> GetAll(
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 0,
        [FromQuery] int size = 10,
        [FromQuery] string sortBy = "createdAt",
        [FromQuery] string sortDir = "desc",
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(0, page);
        var safeSize = Math.Clamp(size, 1, 200);

        var query = _dbContext.Combos.Where(x => !(x.IsDeleted ?? false));

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(x => x.Status.ToLower() == status.ToLower());
        }

        if (fromDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(x => x.CreatedAt <= toDate.Value);
        }

        var desc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = sortBy.Trim().ToLower() switch
        {
            "starttime" => desc ? query.OrderByDescending(x => x.StartTime) : query.OrderBy(x => x.StartTime),
            "endtime" => desc ? query.OrderByDescending(x => x.EndTime) : query.OrderBy(x => x.EndTime),
            _ => desc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
        };

        var totalElements = await query.LongCountAsync(cancellationToken);
        var combos = await query.Skip(safePage * safeSize).Take(safeSize).ToListAsync(cancellationToken);

        var comboIds = combos.Select(x => x.Id).ToList();
        var items = await _dbContext.ComboItems
            .Include(x => x.Product)
            .Include(x => x.ProductVariant)
            .Where(x => comboIds.Contains(x.ComboId))
            .ToListAsync(cancellationToken);

        var resultItems = combos.Select(combo =>
        {
            var comboItems = items.Where(x => x.ComboId == combo.Id).Select(MapComboItem).ToList();
            return MapCombo(combo, comboItems);
        }).ToList();

        return Ok(new ApiResponse<object>
        {
            Result = new
            {
                items = resultItems,
                page = safePage,
                size = safeSize,
                totalElements,
                totalPages = (int)Math.Ceiling(totalElements / (double)safeSize)
            }
        });
    }

    [HttpGet("{comboId}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetById(string comboId, CancellationToken cancellationToken)
    {
        var result = await BuildComboResponse(comboId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = result });
    }

    [HttpGet("available")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetAvailable([FromQuery] DateTime? currentTime, CancellationToken cancellationToken)
    {
        var now = currentTime ?? DateTime.UtcNow;

        var combos = await _dbContext.Combos
            .Where(x => !(x.IsDeleted ?? false)
                && x.Status == "ACTIVE"
                && !x.IsManuallyDisabled
                && x.StartTime <= now
                && x.EndTime >= now)
            .ToListAsync(cancellationToken);

        var results = new List<object>();
        foreach (var combo in combos)
        {
            var stock = await CheckComboStockInternal(combo.Id, cancellationToken);
            if (stock.isAvailable)
            {
                var detail = await BuildComboResponse(combo.Id, cancellationToken);
                results.Add(detail);
            }
        }

        return Ok(new ApiResponse<List<object>> { Result = results });
    }

    [HttpPost("validate")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> Validate([FromBody] ComboValidateRequest request, CancellationToken cancellationToken)
    {
        var combo = await _dbContext.Combos
            .Include(x => x.ComboItems)
            .ThenInclude(x => x.ProductVariant)
            .FirstOrDefaultAsync(x => x.Id == request.ComboId && !(x.IsDeleted ?? false), cancellationToken);

        if (combo is null)
        {
            return Ok(new ApiResponse<object> { Result = new { isValid = false, reason = "COMBO_NOT_FOUND" } });
        }

        if (!string.Equals(combo.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) || combo.IsManuallyDisabled)
        {
            return Ok(new ApiResponse<object> { Result = new { isValid = false, reason = "NOT_ACTIVE" } });
        }

        var now = DateTime.UtcNow;
        if (combo.StartTime > now || combo.EndTime < now)
        {
            return Ok(new ApiResponse<object> { Result = new { isValid = false, reason = "EXPIRED" } });
        }

        var cartMap = request.CartItems
            .GroupBy(x => x.SkuId)
            .ToDictionary(x => x.Key, x => x.Sum(y => y.Quantity));

        foreach (var comboItem in combo.ComboItems)
        {
            if (comboItem.ProductVariantId is not null)
            {
                if (!cartMap.TryGetValue(comboItem.ProductVariantId, out var qty) || qty < comboItem.RequiredQuantity)
                {
                    return Ok(new ApiResponse<object> { Result = new { isValid = false, reason = "NOT_MATCH_RULE" } });
                }
            }
        }

        var stock = await CheckComboStockInternal(combo.Id, cancellationToken);
        if (!stock.isAvailable)
        {
            return Ok(new ApiResponse<object> { Result = new { isValid = false, reason = "OUT_OF_STOCK" } });
        }

        var discount = CalculateComboDiscount(combo, combo.ComboItems);
        return Ok(new ApiResponse<object> { Result = new { isValid = true, discountAmount = discount } });
    }

    [HttpPost("check-stock")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> CheckStock([FromQuery] string comboId, CancellationToken cancellationToken)
    {
        var stock = await CheckComboStockInternal(comboId, cancellationToken);
        return Ok(new ApiResponse<object> { Result = stock });
    }

    [HttpDelete("{comboId}")]
    [Authorize(Roles = "OPERATION,ADMIN")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(string comboId, CancellationToken cancellationToken)
    {
        var combo = await _dbContext.Combos.FirstOrDefaultAsync(x => x.Id == comboId && !(x.IsDeleted ?? false), cancellationToken);
        if (combo is null)
        {
            throw new AppException("COMBO_NOT_FOUND", "Combo not found.", HttpStatusCode.NotFound);
        }

        combo.IsDeleted = true;
        combo.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Message = "Combo deleted successfully",
            Result = null
        });
    }

    private async Task<object> BuildComboResponse(string comboId, CancellationToken cancellationToken)
    {
        var combo = await _dbContext.Combos
            .FirstOrDefaultAsync(x => x.Id == comboId && !(x.IsDeleted ?? false), cancellationToken);

        if (combo is null)
        {
            throw new AppException("COMBO_NOT_FOUND", "Combo not found.", HttpStatusCode.NotFound);
        }

        var items = await _dbContext.ComboItems
            .Include(x => x.Product)
            .Include(x => x.ProductVariant)
            .Where(x => x.ComboId == comboId)
            .ToListAsync(cancellationToken);

        var mappedItems = items.Select(MapComboItem).ToList();
        return MapCombo(combo, mappedItems);
    }

    private static object MapCombo(Combo combo, List<object> comboItems)
    {
        return new
        {
            id = combo.Id,
            name = combo.Name,
            description = combo.Description,
            discountType = combo.DiscountType,
            discountValue = combo.DiscountValue,
            startTime = combo.StartTime,
            endTime = combo.EndTime,
            status = combo.Status,
            isManuallyDisabled = combo.IsManuallyDisabled,
            createdAt = combo.CreatedAt,
            updatedAt = combo.UpdatedAt,
            comboItems
        };
    }

    private static object MapComboItem(ComboItem item)
    {
        return new
        {
            id = item.Id,
            productId = item.ProductId,
            productName = item.Product?.Name,
            skuId = item.ProductVariantId,
            skuLabel = item.ProductVariant is null ? null : $"{item.ProductVariant.ColorName}-{item.ProductVariant.SizeLabel}",
            requiredQuantity = item.RequiredQuantity
        };
    }

    private async Task<List<ComboItem>> BuildComboItems(string comboId, List<ComboItemRequest>? requests, CancellationToken cancellationToken)
    {
        var result = new List<ComboItem>();

        foreach (var request in requests ?? new List<ComboItemRequest>())
        {
            if (string.IsNullOrWhiteSpace(request.ProductId) && string.IsNullOrWhiteSpace(request.SkuId))
            {
                throw new AppException("FIELD_MISSING", "productId or skuId is required for combo item.", HttpStatusCode.BadRequest);
            }

            ProductVariant? variant = null;
            if (!string.IsNullOrWhiteSpace(request.SkuId))
            {
                variant = await _dbContext.ProductVariants.FirstOrDefaultAsync(x => x.Id == request.SkuId && !(x.IsDeleted ?? false), cancellationToken);
                if (variant is null)
                {
                    throw new AppException("PRODUCT_VARIANT_NOT_FOUND", "Variant not found.", HttpStatusCode.NotFound);
                }
            }

            result.Add(new ComboItem
            {
                Id = Guid.NewGuid().ToString(),
                ComboId = comboId,
                ProductId = request.ProductId ?? variant?.ProductId,
                ProductVariantId = request.SkuId,
                RequiredQuantity = request.RequiredQuantity ?? 1
            });
        }

        return result;
    }

    private async Task<(bool isAvailable, List<object> failedItems)> CheckComboStockInternal(string comboId, CancellationToken cancellationToken)
    {
        var combo = await _dbContext.Combos
            .Include(x => x.ComboItems)
            .FirstOrDefaultAsync(x => x.Id == comboId && !(x.IsDeleted ?? false), cancellationToken);

        if (combo is null)
        {
            throw new AppException("COMBO_NOT_FOUND", "Combo not found.", HttpStatusCode.NotFound);
        }

        var failedItems = new List<object>();

        foreach (var item in combo.ComboItems.Where(x => x.ProductVariantId is not null))
        {
            var inventory = await _dbContext.Inventories.FirstOrDefaultAsync(x => x.ProductVariantId == item.ProductVariantId, cancellationToken);
            var available = (inventory?.Quantity ?? 0) - (inventory?.ReservedQuantity ?? 0);

            if (available < item.RequiredQuantity)
            {
                failedItems.Add(new
                {
                    skuId = item.ProductVariantId,
                    requiredQuantity = item.RequiredQuantity,
                    availableQuantity = available
                });
            }
        }

        return (failedItems.Count == 0, failedItems);
    }

    private static decimal CalculateComboDiscount(Combo combo, IEnumerable<ComboItem> items)
    {
        if (string.Equals(combo.DiscountType, "FIXED_AMOUNT", StringComparison.OrdinalIgnoreCase))
        {
            return combo.DiscountValue;
        }

        var comboItemsTotal = items.Sum(x => (x.ProductVariant?.Price ?? 0m) * x.RequiredQuantity);
        return Math.Round(comboItemsTotal * combo.DiscountValue / 100m, 2, MidpointRounding.AwayFromZero);
    }

    private static string ResolveComboStatus(DateTime startTime, DateTime endTime, bool isManuallyDisabled)
    {
        if (isManuallyDisabled)
        {
            return "INACTIVE";
        }

        var now = DateTime.UtcNow;
        if (now < startTime)
        {
            return "SCHEDULED";
        }

        if (now > endTime)
        {
            return "EXPIRED";
        }

        return "ACTIVE";
    }

    private static void ValidateComboRequest(ComboUpsertRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new AppException("FIELD_MISSING", "name is required.", HttpStatusCode.BadRequest);
        }

        if (request.DiscountValue <= 0)
        {
            throw new AppException("INVALID_DISCOUNT", "discountValue must be greater than zero.", HttpStatusCode.BadRequest);
        }

        if (request.StartTime >= request.EndTime)
        {
            throw new AppException("INVALID_DATE_RANGE", "startTime must be before endTime.", HttpStatusCode.BadRequest);
        }

        if (string.Equals(request.DiscountType, "PERCENT", StringComparison.OrdinalIgnoreCase) && request.DiscountValue > 100)
        {
            throw new AppException("INVALID_DISCOUNT", "PERCENT discount cannot be greater than 100.", HttpStatusCode.BadRequest);
        }
    }

    public sealed class ComboUpsertRequest
    {
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public string DiscountType { get; set; } = "PERCENT";

        public decimal DiscountValue { get; set; }

        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public bool? IsManuallyDisabled { get; set; }

        public List<ComboItemRequest>? ComboItems { get; set; }
    }

    public sealed class ComboItemRequest
    {
        public string? ProductId { get; set; }

        public string? SkuId { get; set; }

        public int? RequiredQuantity { get; set; }
    }

    public sealed class ComboStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    public sealed class ComboValidateRequest
    {
        public string ComboId { get; set; } = string.Empty;

        public List<CartItemRequest> CartItems { get; set; } = new();
    }

    public sealed class CartItemRequest
    {
        public string SkuId { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}


