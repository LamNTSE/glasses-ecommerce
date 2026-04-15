using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpticalStore.API.Requests.Auth;
using OpticalStore.API.Responses.Auth;
using OpticalStore.BLL.DTOs;
using OpticalStore.BLL.Services.Interfaces;

namespace OpticalStore.API.Controllers
{
    [ApiController]
    [Route("auth")]
    [ApiExplorerSettings(GroupName = "v1")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IMapper _mapper;

        public AuthController(IAuthService authService, IMapper mapper)
        {
            _authService = authService;
            _mapper = mapper;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var dto = _mapper.Map<RegisterRequestDto>(request);
            await _authService.RegisterAsync(dto);
            return Ok();
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            var dto = _mapper.Map<LoginRequestDto>(request);
            var result = await _authService.LoginAsync(dto);
            return Ok(_mapper.Map<AuthResponse>(result));
        }

        [HttpPost("refresh-token")]
        public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            var result = await _authService.RefreshTokenAsync(request.RefreshToken);
            return Ok(_mapper.Map<AuthResponse>(result));
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            await _authService.RevokeRefreshTokenAsync(userId.Value);
            return Ok();
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<UserResponse?>> Me()
        {
            var userId = GetCurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var currentUser = await _authService.GetCurrentUserAsync(userId.Value);
            if (currentUser == null)
            {
                return NotFound();
            }

            return Ok(_mapper.Map<UserResponse>(currentUser));
        }

        private long? GetCurrentUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

            return long.TryParse(sub, out var userId) ? userId : null;
        }

    }
}
