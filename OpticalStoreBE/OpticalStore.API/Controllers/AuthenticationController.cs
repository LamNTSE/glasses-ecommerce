using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Requests.Auth;
using OpticalStore.API.Responses;
using OpticalStore.BLL.DTOs.Auth;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers;

[ApiController]
[Route("auth")]
[Tags("1. Authentication")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthResultDto>>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(
            new LoginRequestDto
            {
                Username = request.Username,
                Password = request.Password
            },
            cancellationToken);

        return Ok(new ApiResponse<AuthResultDto>
        {
            Result = result
        });
    }

    [HttpPost("introspect")]
    [ProducesResponseType(typeof(ApiResponse<IntrospectResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<IntrospectResultDto>>> Introspect([FromBody] TokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.IntrospectAsync(
            new TokenRequestDto { Token = request.Token },
            cancellationToken);

        return Ok(new ApiResponse<IntrospectResultDto>
        {
            Result = result
        });
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<AuthResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthResultDto>>> Refresh([FromBody] TokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshAsync(
            new TokenRequestDto { Token = request.Token },
            cancellationToken);

        return Ok(new ApiResponse<AuthResultDto>
        {
            Result = result
        });
    }

    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> Logout([FromBody] TokenRequest request, CancellationToken cancellationToken)
    {
        await _authService.LogoutAsync(
            new TokenRequestDto { Token = request.Token },
            cancellationToken);

        return Ok(new ApiResponse<object>
        {
            Message = "Logout successful",
            Result = null
        });
    }
}
