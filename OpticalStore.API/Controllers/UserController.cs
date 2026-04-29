using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Mappings;
using OpticalStore.API.Requests.Users;
using OpticalStore.API.Responses;
using OpticalStore.API.Swagger;
using OpticalStore.BLL.DTOs.Users;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("users")]
[Tags("02. Users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IWebHostEnvironment _environment;

    // Khoi tao controller va gan service xu ly user.
    public UsersController(IUserService userService, IWebHostEnvironment environment)
    {
        _userService = userService;
        _environment = environment;
    }

    /// <summary>
    /// Đăng ký: <c>application/json</c> (body là <see cref="UserRegistrationRequest"/>)
    /// hoặc <c>multipart/form-data</c> với part <c>registration</c> (JSON string) và tùy chọn file <c>avatar</c>.
    /// Một endpoint tránh 415 khi client/proxy hoặc bản deploy cũ chỉ hỗ trợ một kiểu.
    /// </summary>
    // Xu ly dang ky tu JSON hoac multipart de ho tro nhieu client.
    [HttpPost("registration")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> Register(CancellationToken cancellationToken)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        UserRegistrationRequest request;
        UserRegistrationDto dto;

        // Neu request la form-data thi lay payload JSON tu field registration.
        if (Request.HasFormContentType)
        {
            if (!Request.Form.TryGetValue("registration", out var regValues))
            {
                throw new AppException(
                    "INVALID_REGISTRATION",
                    "Thiếu trường registration (chuỗi JSON) trong form.",
                    HttpStatusCode.BadRequest);
            }

            var registration = regValues.ToString();
            if (string.IsNullOrWhiteSpace(registration))
            {
                throw new AppException("INVALID_REGISTRATION", "registration rỗng.", HttpStatusCode.BadRequest);
            }

            request = JsonSerializer.Deserialize<UserRegistrationRequest>(registration, jsonOptions)
                ?? throw new ArgumentException("Invalid registration payload.");

            dto = request.ToDto();

            // Luu avatar neu client gui kem file trong form.
            var avatar = Request.Form.Files.GetFile("avatar");
            if (avatar is { Length: > 0 })
            {
                dto.ImageUrl = await UserAvatarStorage.SaveAsync(avatar, _environment, cancellationToken);
            }
        }
        else
        {

            // Neu khong phai form-data thi doc truc tiep body JSON.
            request = await JsonSerializer.DeserializeAsync<UserRegistrationRequest>(Request.Body, jsonOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new ArgumentException("Invalid registration payload.");

            dto = request.ToDto();
        }

        var result = await _userService.RegisterAsync(dto, cancellationToken);

        return Ok(new ApiResponse<UserResponseDto>
        {
            Message = "User registered successfully",
            Result = result
        });
    }

    // Lay danh sach nguoi dung theo role.
    [HttpGet]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<List<UserResponseDto>>>> GetUsers([FromQuery] string role, CancellationToken cancellationToken)
    {
        var result = await _userService.GetUsersAsync(role, cancellationToken);
        return Ok(new ApiResponse<List<UserResponseDto>> { Result = result });
    }

    // Lay ho so cua nguoi dung hien tai.
    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetMe(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("userId") ?? string.Empty;
        var result = await _userService.GetMyProfileAsync(userId, cancellationToken);
        return Ok(new ApiResponse<UserResponseDto> { Result = result });
    }

    // Lay thong tin user theo id.
    [HttpGet("{userId}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetById(string userId, CancellationToken cancellationToken)
    {
        var result = await _userService.GetByIdAsync(userId, cancellationToken);
        return Ok(new ApiResponse<UserResponseDto> { Result = result });
    }

    // Cap nhat ho so cua chinh nguoi dung.
    [HttpPut("me")]
    [Authorize(Roles = "CUSTOMER")]
    [SwaggerMultipartJsonPart("data", typeof(UserUpdateRequest))]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> UpdateMe(
        [FromForm] string data,
        IFormFile? avatar,
        CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<UserUpdateRequest>(data, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ArgumentException("Invalid data payload.");

        var dto = request.ToDto();

        // Luu avatar moi neu client gui kem file.
        if (avatar is { Length: > 0 })
        {
            dto.ImageUrl = await UserAvatarStorage.SaveAsync(avatar, _environment, cancellationToken);
        }

        var userId = User.FindFirstValue("userId") ?? string.Empty;

        var result = await _userService.UpdateMyProfileAsync(userId, dto, cancellationToken);

        return Ok(new ApiResponse<UserResponseDto> { Result = result });
    }

    // Cap nhat avatar cua nguoi dung hien tai.
    [HttpPatch("me/avatar")]
    [Authorize(Roles = "CUSTOMER")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> UpdateMyAvatar(
        IFormFile avatar,
        CancellationToken cancellationToken)
    {
        if (avatar is not { Length: > 0 })
        {
            throw new AppException("AVATAR_REQUIRED", "Vui lòng gửi file ảnh đại diện.", HttpStatusCode.BadRequest);
        }

        // Luu file avatar truoc khi cap nhat profile.
        var path = await UserAvatarStorage.SaveAsync(avatar, _environment, cancellationToken);
        var userId = User.FindFirstValue("userId") ?? string.Empty;
        var result = await _userService.UpdateMyProfileAsync(userId, new UserUpdateDto { ImageUrl = path }, cancellationToken);
        return Ok(new ApiResponse<UserResponseDto> { Result = result });
    }

    // Cap nhat trang thai user.
    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> UpdateStatus(
        string id,
        [FromQuery(Name = "UserStatus")] string userStatus,
        CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateStatusAsync(id, userStatus, cancellationToken);
        return Ok(new ApiResponse<UserResponseDto> { Result = result });
    }

    // Cap nhat role cua user.
    [HttpPut("{id}/role")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> UpdateRole(
        string id,
        [FromQuery] string role,
        CancellationToken cancellationToken)
    {
        var result = await _userService.UpdateRoleAsync(id, role, cancellationToken);
        return Ok(new ApiResponse<UserResponseDto> { Result = result });
    }

    // Xoa user khoi he thong.
    [HttpDelete("{userId}")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(string userId, CancellationToken cancellationToken)
    {
        await _userService.DeleteAsync(userId, cancellationToken);
        return Ok(new ApiResponse<string>
        {
            Message = "User has been deleted successfully",
            Result = "User has been deleted successfully"
        });
    }
}
