using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Permissions;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class PermissionService : IPermissionService
{
    private readonly OpticalStoreDbContext _dbContext;

    public PermissionService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PermissionDto> CreateAsync(CreatePermissionDto request, CancellationToken cancellationToken = default)
    {
        var existed = await _dbContext.Permissions.AnyAsync(x => x.Name == request.Name, cancellationToken);
        if (existed)
        {
            throw new AppException("PERMISSION_EXISTED", "Permission already exists.", HttpStatusCode.BadRequest);
        }

        var permission = new Permission
        {
            Name = request.Name,
            Description = request.Description
        };

        _dbContext.Permissions.Add(permission);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new PermissionDto
        {
            Name = permission.Name,
            Description = permission.Description
        };
    }

    public async Task<List<PermissionDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.Permissions
            .Select(x => new PermissionDto
            {
                Name = x.Name,
                Description = x.Description
            })
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteAsync(string permissionName, CancellationToken cancellationToken = default)
    {
        var permission = await _dbContext.Permissions.FirstOrDefaultAsync(x => x.Name == permissionName, cancellationToken);
        if (permission is null)
        {
            throw new AppException("PERMISSION_NOT_FOUND", "Permission not found.", HttpStatusCode.NotFound);
        }

        _dbContext.Permissions.Remove(permission);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
