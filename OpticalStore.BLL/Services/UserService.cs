using System.Net;
using BCrypt.Net;
using OpticalStore.BLL.DTOs.Common;
using OpticalStore.BLL.DTOs.Users;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.Entities;
using OpticalStore.DAL.Repositories.Interfaces;

namespace OpticalStore.BLL.Services;

public sealed class UserService : IUserService
{
    private const string DefaultAvatarUrl = "";

    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;

    // Khoi tao service user voi repository nguoi dung va role.
    public UserService(IUserRepository userRepository, IRoleRepository roleRepository)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
    }

    // Dang ky nguoi dung moi voi role CUSTOMER mac dinh.
    public async Task<UserResponseDto> RegisterAsync(UserRegistrationDto request, CancellationToken cancellationToken = default)
    {
        var existed = await _userRepository.ExistsByUsernameAsync(request.Username, cancellationToken);
        if (existed)
        {
            throw new AppException("USER_EXISTED", "Username already existed.", HttpStatusCode.BadRequest);
        }

        var customerRole = await _roleRepository.GetByNameWithPermissionsAsync("CUSTOMER", cancellationToken);
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
            Phone = request.Phone,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Dob = request.Dob,
            Status = "ACTIVE",
            ImageUrl = !string.IsNullOrWhiteSpace(request.ImageUrl) ? request.ImageUrl : null
        };

        user.RoleNames.Add(customerRole);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(user.Id, cancellationToken);
    }

    // Lay danh sach user, co the loc theo role.
    public async Task<List<UserResponseDto>> GetUsersAsync(string role, CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.GetUsersWithSecurityAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(role))
        {
            users = users
                .Where(x => x.RoleNames.Any(r => string.Equals(r.Name, role, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return users.Select(MapUser).ToList();
    }

    // Lay profile cua chinh nguoi dung.
    public async Task<UserResponseDto> GetMyProfileAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await GetByIdAsync(userId, cancellationToken);
    }

    // Lay user theo id va kiem tra ton tai.
    public async Task<UserResponseDto> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithSecurityAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new AppException("USER_NOT_EXISTED", "User not found.", HttpStatusCode.NotFound);
        }

        return MapUser(user);
    }

    // Cap nhat ho so cua chinh nguoi dung.
    public async Task<UserResponseDto> UpdateMyProfileAsync(string userId, UserUpdateDto request, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithSecurityAsync(userId, cancellationToken);

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

        // Chi cap nhat avatar khi request co gia tri moi.
        if (!string.IsNullOrWhiteSpace(request.ImageUrl))
            user.ImageUrl = request.ImageUrl;

        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapUser(user);
    }

    // Cap nhat trang thai user.
    public async Task<UserResponseDto> UpdateStatusAsync(string userId, string status, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithSecurityAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new AppException("USER_NOT_EXISTED", "User not found.", HttpStatusCode.NotFound);
        }

        user.Status = status;
        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapUser(user);
    }

    // Cap nhat role chinh cho user.
    public async Task<UserResponseDto> UpdateRoleAsync(string userId, string role, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdWithSecurityAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new AppException("USER_NOT_EXISTED", "User not found.", HttpStatusCode.NotFound);
        }

        var targetRole = await _roleRepository.GetByNameWithPermissionsAsync(role, cancellationToken);

        if (targetRole is null)
        {
            throw new AppException("ROLE_NOT_FOUND", "Role not found.", HttpStatusCode.NotFound);
        }

        user.RoleNames.Clear();
        user.RoleNames.Add(targetRole);

        await _userRepository.SaveChangesAsync(cancellationToken);

        return MapUser(user);
    }

    // Xoa user khoi he thong.
    public async Task DeleteAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            throw new AppException("USER_NOT_EXISTED", "User not found.", HttpStatusCode.NotFound);
        }

        _userRepository.Remove(user);
        await _userRepository.SaveChangesAsync(cancellationToken);
    }

    // Chuyen entity user sang DTO de tra ve API.
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
