using System.Net;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Users;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class UserService : IUserService
{
    private const string DefaultAvatarUrl = "https://i.pinimg.com/1200x/3a/61/2c/3a612c76f58249ad16349f0cebc9d2b6.jpg";

    private readonly OpticalStoreDbContext _dbContext;

    public UserService(OpticalStoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserResponseDto> RegisterAsync(UserRegistrationDto request, CancellationToken cancellationToken = default)
    {
        var existed = await _dbContext.Users.AnyAsync(x => x.Username == request.Username, cancellationToken);
        if (existed)
        {
            throw new AppException("USER_EXISTED", "Username already existed.", HttpStatusCode.BadRequest);
        }

        var customerRole = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Name == "CUSTOMER", cancellationToken);
        if (customerRole is null)
        {
            throw new AppException("ROLE_NOT_FOUND", "Default CUSTOMER role not found.", HttpStatusCode.BadRequest);
        }

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Username = request.Username,
            Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Dob = request.Dob,
            Status = "ACTIVE",
            ImageUrl = DefaultAvatarUrl
        };

        user.RoleNames.Add(customerRole);

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(user.Id, cancellationToken);
    }

    public async Task<List<UserResponseDto>> GetUsersAsync(string role, CancellationToken cancellationToken = default)
    {
        var users = await _dbContext.Users
            .Include(x => x.RoleNames)
            .ThenInclude(x => x.PermissionsNames)
            .ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(role))
        {
            users = users
                .Where(x => x.RoleNames.Any(r => string.Equals(r.Name, role, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return users.Select(MapUser).ToList();
    }

    public async Task<UserResponseDto> GetMyProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await GetByIdAsync(userId, cancellationToken);
    }

    public async Task<UserResponseDto> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(x => x.RoleNames)
            .ThenInclude(x => x.PermissionsNames)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new AppException("USER_NOT_EXISTED", "User not found.", HttpStatusCode.NotFound);
        }

        return MapUser(user);
    }

    public async Task<UserResponseDto> UpdateMyProfileAsync(string userId, UserUpdateDto request, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(x => x.RoleNames)
            .ThenInclude(x => x.PermissionsNames)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new AppException("USER_NOT_EXISTED", "User not found.", HttpStatusCode.NotFound);
        }

        if (!string.IsNullOrWhiteSpace(request.Password))
        {
            user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password);
        }

        user.FirstName = request.FirstName ?? user.FirstName;
        user.LastName = request.LastName ?? user.LastName;
        user.Dob = request.Dob ?? user.Dob;
        user.Email = request.Email ?? user.Email;
        user.Phone = request.Phone ?? user.Phone;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapUser(user);
    }

    public async Task<UserResponseDto> UpdateStatusAsync(string userId, string status, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(x => x.RoleNames)
            .ThenInclude(x => x.PermissionsNames)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new AppException("USER_NOT_EXISTED", "User not found.", HttpStatusCode.NotFound);
        }

        user.Status = status;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapUser(user);
    }

    public async Task<UserResponseDto> UpdateRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users
            .Include(x => x.RoleNames)
            .ThenInclude(x => x.PermissionsNames)
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new AppException("USER_NOT_EXISTED", "User not found.", HttpStatusCode.NotFound);
        }

        var targetRole = await _dbContext.Roles
            .Include(x => x.PermissionsNames)
            .FirstOrDefaultAsync(x => x.Name == role, cancellationToken);

        if (targetRole is null)
        {
            throw new AppException("ROLE_NOT_FOUND", "Role not found.", HttpStatusCode.NotFound);
        }

        user.RoleNames.Clear();
        user.RoleNames.Add(targetRole);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapUser(user);
    }

    public async Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null)
        {
            throw new AppException("USER_NOT_EXISTED", "User not found.", HttpStatusCode.NotFound);
        }

        _dbContext.Users.Remove(user);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static UserResponseDto MapUser(User user)
    {
        return new UserResponseDto
        {
            Id = user.Id,
            Username = user.Username,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Dob = user.Dob,
            ImageUrl = user.ImageUrl,
            Email = user.Email,
            Phone = user.Phone,
            Roles = user.RoleNames.Select(role => new RoleDto
            {
                Name = role.Name,
                Description = role.Description,
                Permissions = role.PermissionsNames.Select(permission => new PermissionDto
                {
                    Name = permission.Name,
                    Description = permission.Description
                }).ToList()
            }).ToList()
        };
    }
}
