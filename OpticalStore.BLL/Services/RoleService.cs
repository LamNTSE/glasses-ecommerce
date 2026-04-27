using System.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Roles;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class RoleService : IRoleService
{
    private readonly OpticalStoreDbContext _dbContext;

    public RoleService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RoleDto> CreateAsync(CreateRoleDto request, CancellationToken cancellationToken = default)
    {
        var existed = await _dbContext.Roles.AnyAsync(x => x.Name == request.Name, cancellationToken);
        if (existed)
        {
            throw new AppException("ROLE_EXISTED", "Role already exists.", HttpStatusCode.BadRequest);
        }

        var permissions = await _dbContext.Permissions
            .Where(x => request.Permissions.Contains(x.Name))
            .ToListAsync(cancellationToken);

        var role = new Role
        {
            Name = request.Name,
            Description = request.Description
        };

        foreach (var permission in permissions)
        {
            role.PermissionsNames.Add(permission);
        }

        _dbContext.Roles.Add(role);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapRole(role);
    }

    public async Task<List<RoleDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var roles = await _dbContext.Roles
            .Include(x => x.PermissionsNames)
            .ToListAsync(cancellationToken);

        return roles.Select(MapRole).ToList();
    }

    public async Task DeleteAsync(string roleName, CancellationToken cancellationToken = default)
    {
        var role = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Name == roleName, cancellationToken);
        if (role is null)
        {
            throw new AppException("ROLE_NOT_FOUND", "Role not found.", HttpStatusCode.NotFound);
        }

        _dbContext.Roles.Remove(role);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static RoleDto MapRole(Role role)
    {
        return new RoleDto
        {
            Name = role.Name,
            Description = role.Description,
            Permissions = role.PermissionsNames.Select(x => new PermissionDto
            {
                Name = x.Name,
                Description = x.Description
            }).ToList()
        };
    }
}
