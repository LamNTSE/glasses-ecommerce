using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Mappings;
using OpticalStore.API.Requests.Users;
using OpticalStore.API.Responses;
using OpticalStore.API.Swagger;
using OpticalStore.BLL.DTOs.Users;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("users")]
[Tags("02. Users")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("registration")]
    [AllowAnonymous]
    [Consumes("multipart/form-data", "application/json")]
    [SwaggerMultipartJsonPart("UserInfor", typeof(UserRegistrationRequest))]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> Register(
        [FromForm(Name = "UserInfor")] string? userInfor,
        IFormFile? imageUrl,
        CancellationToken cancellationToken)
    {
        var userInforPayload = userInfor;

        if (string.IsNullOrWhiteSpace(userInforPayload) && Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            userInforPayload = form["UserInfor"].FirstOrDefault();

            // Some clients send JSON part with content-type application/json as a file-like part.
            if (string.IsNullOrWhiteSpace(userInforPayload))
            {
                var jsonPart = form.Files.GetFile("UserInfor");
                if (jsonPart is not null)
                {
                    using var reader = new StreamReader(jsonPart.OpenReadStream());
                    userInforPayload = await reader.ReadToEndAsync(cancellationToken);
                }
            }

            imageUrl ??= form.Files.GetFile(nameof(imageUrl));
        }

        if (string.IsNullOrWhiteSpace(userInforPayload) && Request.HasJsonContentType())
        {
            using var reader = new StreamReader(Request.Body);
            userInforPayload = await reader.ReadToEndAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(userInforPayload))
        {
            ModelState.AddModelError("UserInfor", "The userInfor field is required.");
            return ValidationProblem(ModelState);
        }

        var request = JsonSerializer.Deserialize<UserRegistrationRequest>(userInforPayload, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ArgumentException("Invalid UserInfor payload.");

        var result = await _userService.RegisterAsync(request.ToDto(), cancellationToken);

        return Ok(new ApiResponse<UserResponseDto>
        {
            Message = "User registered successfully",
            Result = result
        });
    }

    [HttpGet]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<List<UserResponseDto>>>> GetUsers([FromQuery] string role, CancellationToken cancellationToken)
    {
        var result = await _userService.GetUsersAsync(role, cancellationToken);
        return Ok(new ApiResponse<List<UserResponseDto>> { Result = result });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetMe(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue("userId") ?? string.Empty;
        var result = await _userService.GetMyProfileAsync(userId, cancellationToken);
        return Ok(new ApiResponse<UserResponseDto> { Result = result });
    }

    [HttpGet("{userId}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> GetById(string userId, CancellationToken cancellationToken)
    {
        var result = await _userService.GetByIdAsync(userId, cancellationToken);
        return Ok(new ApiResponse<UserResponseDto> { Result = result });
    }

    [HttpPut("me")]
    [Authorize(Roles = "CUSTOMER")]
    [SwaggerMultipartJsonPart("data", typeof(UserUpdateRequest))]
    public async Task<ActionResult<ApiResponse<UserResponseDto>>> UpdateMe(
        [FromForm] string data,
        IFormFile? imageUrl,
        CancellationToken cancellationToken)
    {
        _ = imageUrl;

        var request = JsonSerializer.Deserialize<UserUpdateRequest>(data, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new ArgumentException("Invalid data payload.");

        var userId = User.FindFirstValue("userId") ?? string.Empty;

        var result = await _userService.UpdateMyProfileAsync(userId, request.ToDto(), cancellationToken);

        return Ok(new ApiResponse<UserResponseDto> { Result = result });
    }

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

    [HttpDelete("{userId}")]
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
