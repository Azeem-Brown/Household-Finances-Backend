using System.Security.Claims;
using System.Text;
using HouseholdFinances.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Issues and validates the API's own HMAC-signed JWTs.
/// </summary>
/// <remarks>
/// Access and refresh tokens are separated in two independent ways: they carry different audiences
/// and a different <c>token_use</c> claim. Both are checked on every validation, so an access token
/// cannot be replayed as a refresh token or vice versa even though both are signed with the same
/// key. The signing key is read from configuration and is never logged.
/// </remarks>
public sealed class ApiTokenService : IApiTokenService
{
    /// <summary>Claim type that distinguishes an access token from a refresh token.</summary>
    public const string TokenUseClaimType = "token_use";

    /// <summary>Value of <see cref="TokenUseClaimType"/> for access tokens.</summary>
    public const string AccessTokenUse = "access";

    /// <summary>Value of <see cref="TokenUseClaimType"/> for refresh tokens.</summary>
    public const string RefreshTokenUse = "refresh";

    private readonly JwtOptions _options;
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SymmetricSecurityKey _signingKey;
    private readonly SigningCredentials _signingCredentials;

    /// <summary>Creates the service over the supplied JWT options.</summary>
    /// <param name="options">The issuer, audiences, signing key, and lifetimes.</param>
    /// <exception cref="InvalidOperationException">The signing key is not configured.</exception>
    public ApiTokenService(JwtOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));

        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            throw new InvalidOperationException("The JWT signing key is not configured.");
        }

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        _signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);
    }

    /// <inheritdoc />
    public ApiToken CreateAccessToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddMinutes(_options.AccessTokenLifetimeMinutes);

        var token = CreateToken(user, _options.Audience, AccessTokenUse, issuedAt, expiresAt);

        return new ApiToken(token, expiresAt);
    }

    /// <inheritdoc />
    public ApiToken CreateRefreshToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var issuedAt = DateTime.UtcNow;
        var expiresAt = issuedAt.AddDays(_options.RefreshTokenLifetimeDays);

        var token = CreateToken(user, _options.RefreshAudience, RefreshTokenUse, issuedAt, expiresAt);

        return new ApiToken(token, expiresAt);
    }

    /// <inheritdoc />
    public async Task<Guid> ValidateRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidRefreshTokenException("No refresh token was supplied.");
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.RefreshAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        };

        Microsoft.IdentityModel.Tokens.TokenValidationResult result;
        try
        {
            result = await _handler.ValidateTokenAsync(refreshToken, parameters);
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            throw new InvalidRefreshTokenException("The refresh token failed validation.");
        }

        if (!result.IsValid || result.ClaimsIdentity is null)
        {
            throw new InvalidRefreshTokenException("The refresh token failed validation.");
        }

        var tokenUse = result.ClaimsIdentity.FindFirst(TokenUseClaimType)?.Value;
        if (!string.Equals(tokenUse, RefreshTokenUse, StringComparison.Ordinal))
        {
            throw new InvalidRefreshTokenException("The token is not a refresh token.");
        }

        var subject = result.ClaimsIdentity.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? result.ClaimsIdentity.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(subject, out var userId))
        {
            throw new InvalidRefreshTokenException("The refresh token has no valid user identifier.");
        }

        return userId;
    }

    private string CreateToken(
        User user,
        string audience,
        string tokenUse,
        DateTime issuedAt,
        DateTime expiresAt)
    {
        var userId = user.Id.ToString();

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = _signingCredentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId,
                // The standard scheme-agnostic identifier claim that ICurrentUserService reads.
                [ClaimTypes.NameIdentifier] = userId,
                [JwtRegisteredClaimNames.Name] = user.Name,
                [JwtRegisteredClaimNames.Email] = user.Email,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
                [TokenUseClaimType] = tokenUse,
            },
        };

        return _handler.CreateToken(descriptor);
    }
}
