using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpticalStore.BLL.DTOs;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.Entities;
using OpticalStore.DAL.Entities.Enums;
using OpticalStore.DAL.Repositories.Interfaces;

namespace OpticalStore.BLL.Services
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepository;
        private readonly JwtSettings _jwtSettings;

        public AuthService(IUserRepository userRepository, IOptions<JwtSettings> jwtOptions)
        {
            _userRepository = userRepository;
            _jwtSettings = jwtOptions.Value;
        }

        public async Task RegisterAsync(RegisterRequestDto request)
        {
            var existingUser = await _userRepository.GetByEmailAsync(request.Email);
            if (existingUser != null)
            {
                throw new InvalidOperationException("Email already in use.");
            }

            var existingUsername = await _userRepository.GetByUsernameAsync(request.Username);
            if (existingUsername != null)
            {
                throw new InvalidOperationException("Username already in use.");
            }

            var user = new User
            {
                Id = Guid.NewGuid().ToString("D").ToLowerInvariant(),
                Dob = request.Dob,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName,
                Username = request.Username,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Phone = request.Phone,
                ImageUrl = request.ImageUrl,
                Status = StatusValues.Active
            };

            await _userRepository.AddAsync(user);
            await _userRepository.SaveChangesAsync();
        }

        public async Task<AuthResultDto> LoginAsync(LoginRequestDto request)
        {
            var user = await _userRepository.GetByEmailAsync(request.Email);
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                throw new UnauthorizedAccessException("Invalid email or password.");
            }

            if (!string.Equals(user.Status, StatusValues.Active, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException("User account is inactive.");
            }

            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            return BuildAuthResult(user, accessToken, refreshToken);
        }

        public async Task<AuthResultDto> RefreshTokenAsync(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                throw new UnauthorizedAccessException("Invalid refresh token.");
            }

            var user = await _userRepository.GetByRefreshTokenAsync(refreshToken);
            if (user == null)
            {
                throw new UnauthorizedAccessException("Refresh token is invalid or expired.");
            }

            var newAccessToken = GenerateAccessToken(user);
            var newRefreshToken = GenerateRefreshToken();

            user.RefreshToken = newRefreshToken;
            user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();

            return BuildAuthResult(user, newAccessToken, newRefreshToken);
        }

        public async Task RevokeRefreshTokenAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return;
            }

            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
            _userRepository.Update(user);
            await _userRepository.SaveChangesAsync();
        }

        public async Task<UserDto?> GetCurrentUserAsync(string userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return null;
            }

            return new UserDto
            {
                Id = user.Id,
                Dob = user.Dob,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Username = user.Username,
                Phone = user.Phone,
                ImageUrl = user.ImageUrl,
                Status = user.Status
            };
        }

        private AuthResultDto BuildAuthResult(User user, string accessToken, string refreshToken)
        {
            return new AuthResultDto
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = _jwtSettings.AccessTokenExpirationMinutes * 60,
                User = new UserDto
                {
                    Id = user.Id,
                    Dob = user.Dob,
                    Email = user.Email,
                    FirstName = user.FirstName,
                    LastName = user.LastName,
                    Username = user.Username,
                    Phone = user.Phone,
                    ImageUrl = user.ImageUrl,
                    Status = user.Status
                }
            };
        }

        private string GenerateAccessToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(ClaimTypes.Name, user.Username)
            };

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private static string GenerateRefreshToken()
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
    }
}
