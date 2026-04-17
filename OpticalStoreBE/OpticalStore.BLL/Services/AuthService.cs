using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OpticalStore.BLL.Configuration;
using OpticalStore.BLL.DTOs.Auth;
using OpticalStore.BLL.Exceptions;
using OpticalStore.BLL.Services.Interfaces;
using OpticalStore.DAL.DBContext;
using OpticalStore.DAL.Entities;

namespace OpticalStore.BLL.Services;

public sealed class AuthService : IAuthService
{
    private static readonly JwtSecurityTokenHandler TokenHandler = new();

    private readonly OpticalStoreDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;
    private readonly TokenValidationParameters _tokenValidationParameters;

    public AuthService(OpticalStoreDbContext dbContext, IOptions<JwtOptions> jwtOptions)
    {
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;

        if (string.IsNullOrWhiteSpace(_jwtOptions.Key) || Encoding.UTF8.GetByteCount(_jwtOptions.Key) < 32)
        {
            throw new AppException("CONFIG_INVALID", "Jwt:Key must be at least 32 bytes.", HttpStatusCode.InternalServerError);
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        _tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateIssuer = true,
            ValidIssuer = _jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    }

    public async Task<AuthResultDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        var normalizedUsername = request.Username.Trim();
        var user = await _dbContext.Users
            .Include(x => x.RoleNames)
            .ThenInclude(x => x.PermissionsNames)
            .FirstOrDefaultAsync(x => x.Username == normalizedUsername, cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(user.Password) || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            throw new AppException("UNAUTHENTICATED", "Invalid username or password.", HttpStatusCode.Unauthorized);
        }

        var token = GenerateAccessToken(user);

        return new AuthResultDto
        {
            Token = token,
            Authenticated = true
        };
    }

    public async Task<IntrospectResultDto> IntrospectAsync(TokenRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var principal = await ValidateTokenAsync(request.Token, isRefreshFlow: false, cancellationToken);
            return new IntrospectResultDto { Valid = principal is not null };
        }
        catch
        {
            return new IntrospectResultDto { Valid = false };
        }
    }

    public async Task<AuthResultDto> RefreshAsync(TokenRequestDto request, CancellationToken cancellationToken = default)
    {
        var principal = await ValidateTokenAsync(request.Token, isRefreshFlow: true, cancellationToken);
        var username = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new AppException("UNAUTHENTICATED", "Invalid token subject.", HttpStatusCode.Unauthorized);
        }

        var user = await _dbContext.Users
            .Include(x => x.RoleNames)
            .ThenInclude(x => x.PermissionsNames)
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

        if (user is null)
        {
            throw new AppException("USER_NOT_EXISTED", "User not found.", HttpStatusCode.NotFound);
        }

        var parsedJwt = TokenHandler.ReadJwtToken(request.Token);
        await InvalidateTokenAsync(parsedJwt, cancellationToken);

        return new AuthResultDto
        {
            Token = GenerateAccessToken(user),
            Authenticated = true
        };
    }

    public async Task LogoutAsync(TokenRequestDto request, CancellationToken cancellationToken = default)
    {
        try
        {
            var principal = await ValidateTokenAsync(request.Token, isRefreshFlow: false, cancellationToken);
            if (principal is null)
            {
                return;
            }

            var parsedJwt = TokenHandler.ReadJwtToken(request.Token);
            await InvalidateTokenAsync(parsedJwt, cancellationToken);
        }
        catch (SecurityTokenExpiredException)
        {
            // Keep behavior aligned with Spring service: expired token logout is treated as no-op.
        }
    }

    private string GenerateAccessToken(User user)
    {
        var utcNow = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Username ?? user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("userId", user.Id)
        };

        foreach (var role in user.RoleNames)
        {
            claims.Add(new Claim(ClaimTypes.Role, role.Name));

            foreach (var permission in role.PermissionsNames)
            {
                claims.Add(new Claim("permission", permission.Name));
            }
        }

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key)),
            SecurityAlgorithms.HmacSha512);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: utcNow,
            expires: utcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            signingCredentials: credentials);

        return TokenHandler.WriteToken(token);
    }

    private async Task<ClaimsPrincipal> ValidateTokenAsync(string token, bool isRefreshFlow, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new AppException("UNAUTHENTICATED", "Token is required.", HttpStatusCode.Unauthorized);
        }

        TokenValidationParameters validationParameters;

        if (isRefreshFlow)
        {
            validationParameters = _tokenValidationParameters.Clone();
            validationParameters.ValidateLifetime = false;
        }
        else
        {
            validationParameters = _tokenValidationParameters;
        }

        var principal = TokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

        if (validatedToken is not JwtSecurityToken jwt ||
            !jwt.Header.Alg.Equals(SecurityAlgorithms.HmacSha512, StringComparison.OrdinalIgnoreCase))
        {
            throw new AppException("UNAUTHENTICATED", "Invalid token algorithm.", HttpStatusCode.Unauthorized);
        }

        if (isRefreshFlow)
        {
            var issuedAt = jwt.IssuedAt;
            var refreshWindowEnd = issuedAt.AddDays(_jwtOptions.RefreshTokenExpirationDays);
            if (DateTime.UtcNow > refreshWindowEnd)
            {
                throw new AppException("UNAUTHENTICATED", "Refresh window expired.", HttpStatusCode.Unauthorized);
            }
        }

        var jti = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(jti))
        {
            throw new AppException("UNAUTHENTICATED", "Token ID is missing.", HttpStatusCode.Unauthorized);
        }

        var tokenIsInvalidated = await _dbContext.InvalidatedTokens.AnyAsync(x => x.Id == jti, cancellationToken);
        if (tokenIsInvalidated)
        {
            throw new AppException("UNAUTHENTICATED", "Token is invalidated.", HttpStatusCode.Unauthorized);
        }

        return principal;
    }

    private async Task InvalidateTokenAsync(JwtSecurityToken token, CancellationToken cancellationToken)
    {
        var jti = token.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;
        if (string.IsNullOrWhiteSpace(jti))
        {
            return;
        }

        var existed = await _dbContext.InvalidatedTokens.AnyAsync(x => x.Id == jti, cancellationToken);
        if (existed)
        {
            return;
        }

        var expiry = token.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Exp)?.Value;
        DateTime? expiryTime = null;

        if (long.TryParse(expiry, out var expUnix))
        {
            expiryTime = DateTimeOffset.FromUnixTimeSeconds(expUnix).UtcDateTime;
        }

        _dbContext.InvalidatedTokens.Add(new InvalidatedToken
        {
            Id = jti,
            ExpiryTime = expiryTime
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
