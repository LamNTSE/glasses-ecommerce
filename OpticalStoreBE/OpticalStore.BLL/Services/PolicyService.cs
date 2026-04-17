using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Policies;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class PolicyService : IPolicyService
{
    private readonly OpticalStoreDbContext _dbContext;

    public PolicyService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PolicyResponseDto> CreateAsync(string managerUserId, PolicyUpsertDto request, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(request.EffectiveFrom, request.EffectiveTo);

        var code = request.Code.Trim();
        var title = request.Title.Trim();

        var manager = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == managerUserId, cancellationToken);
        if (manager is null)
        {
            throw new AppException("USER_NOT_EXISTED", "Manager user not found.", HttpStatusCode.NotFound);
        }

        var codeExists = await _dbContext.Policies
            .AnyAsync(x => x.Code.ToLower() == code.ToLower(), cancellationToken);
        if (codeExists)
        {
            throw new AppException("POLICY_CODE_ALREADY_EXISTS", "Policy code already exists.", HttpStatusCode.BadRequest);
        }

        var policy = new Policy
        {
            Code = code,
            Title = title,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            ManagerUserId = manager.Id,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Policies.Add(policy);
        await _dbContext.SaveChangesAsync(cancellationToken);

        policy.ManagerUser = manager;
        return Map(policy);
    }

    public async Task<PolicyResponseDto> UpdateAsync(int id, PolicyUpsertDto request, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(request.EffectiveFrom, request.EffectiveTo);

        var policy = await _dbContext.Policies
            .Include(x => x.ManagerUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (policy is null)
        {
            throw new AppException("POLICY_NOT_FOUND", "Policy not found.", HttpStatusCode.NotFound);
        }

        var code = request.Code.Trim();
        var title = request.Title.Trim();

        var codeExists = await _dbContext.Policies
            .AnyAsync(x => x.Id != id && x.Code.ToLower() == code.ToLower(), cancellationToken);
        if (codeExists)
        {
            throw new AppException("POLICY_CODE_ALREADY_EXISTS", "Policy code already exists.", HttpStatusCode.BadRequest);
        }

        policy.Code = code;
        policy.Title = title;
        policy.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        policy.EffectiveFrom = request.EffectiveFrom;
        policy.EffectiveTo = request.EffectiveTo;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Map(policy);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var policy = await _dbContext.Policies.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (policy is null)
        {
            throw new AppException("POLICY_NOT_FOUND", "Policy not found.", HttpStatusCode.NotFound);
        }

        _dbContext.Policies.Remove(policy);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PolicyResponseDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var policy = await _dbContext.Policies
            .Include(x => x.ManagerUser)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (policy is null)
        {
            throw new AppException("POLICY_NOT_FOUND", "Policy not found.", HttpStatusCode.NotFound);
        }

        return Map(policy);
    }

    public async Task<PagedResultDto<PolicyResponseDto>> GetAllAsync(
        string? keyword,
        DateOnly? effectiveFrom,
        DateOnly? effectiveTo,
        int page,
        int size,
        string sortBy,
        string sortDir,
        CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(page, 0);
        var safeSize = Math.Clamp(size, 1, 200);

        var query = _dbContext.Policies
            .Include(x => x.ManagerUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim().ToLower();
            query = query.Where(x => x.Code.ToLower().Contains(normalizedKeyword) || x.Title.ToLower().Contains(normalizedKeyword));
        }

        if (effectiveFrom.HasValue)
        {
            query = query.Where(x => x.EffectiveFrom >= effectiveFrom.Value);
        }

        if (effectiveTo.HasValue)
        {
            query = query.Where(x => x.EffectiveTo <= effectiveTo.Value);
        }

        var isDesc = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase);
        query = ApplySorting(query, sortBy, isDesc);

        var totalElements = await query.LongCountAsync(cancellationToken);
        var items = await query
            .Skip(safePage * safeSize)
            .Take(safeSize)
            .ToListAsync(cancellationToken);

        return new PagedResultDto<PolicyResponseDto>
        {
            Items = items.Select(Map).ToList(),
            Page = safePage,
            Size = safeSize,
            TotalElements = totalElements,
            TotalPages = (int)Math.Ceiling(totalElements / (double)safeSize)
        };
    }

    private static IQueryable<Policy> ApplySorting(IQueryable<Policy> query, string sortBy, bool isDesc)
    {
        return sortBy.Trim().ToLower() switch
        {
            "code" => isDesc ? query.OrderByDescending(x => x.Code) : query.OrderBy(x => x.Code),
            "title" => isDesc ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
            "effectivefrom" => isDesc ? query.OrderByDescending(x => x.EffectiveFrom) : query.OrderBy(x => x.EffectiveFrom),
            "effectiveto" => isDesc ? query.OrderByDescending(x => x.EffectiveTo) : query.OrderBy(x => x.EffectiveTo),
            _ => isDesc ? query.OrderByDescending(x => x.CreatedAt) : query.OrderBy(x => x.CreatedAt)
        };
    }

    private static void ValidateDateRange(DateOnly? from, DateOnly? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new AppException("INVALID_DATE_RANGE", "effectiveFrom must be less than or equal to effectiveTo.", HttpStatusCode.BadRequest);
        }
    }

    private static PolicyResponseDto Map(Policy policy)
    {
        return new PolicyResponseDto
        {
            Id = policy.Id,
            ManagerUserId = policy.ManagerUserId,
            ManagerUsername = policy.ManagerUser?.Username,
            Code = policy.Code,
            Title = policy.Title,
            Description = policy.Description,
            EffectiveFrom = policy.EffectiveFrom,
            EffectiveTo = policy.EffectiveTo,
            CreatedAt = policy.CreatedAt
        };
    }
}
