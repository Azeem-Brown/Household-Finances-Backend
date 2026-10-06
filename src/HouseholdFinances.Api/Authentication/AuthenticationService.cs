using HouseholdFinances.Api.Models;
using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Orchestrates Google Identity sign-in and API token issuance. Users are provisioned on first
/// successful validation, keyed by the Google subject, and no local password is ever created or
/// accepted.
/// </summary>
public sealed class AuthenticationService : IAuthenticationService
{
    /// <summary>Identifier of significance reported when no Google ID token is supplied.</summary>
    public const string IdTokenIdentifier = "idToken";

    /// <summary>Identifier of significance reported when the Google token is rejected.</summary>
    public const string GoogleTokenIdentifier = "GoogleToken";

    /// <summary>Identifier of significance reported when no refresh token is supplied.</summary>
    public const string RefreshTokenIdentifier = "refreshToken";

    private readonly IGoogleTokenValidator _googleTokenValidator;
    private readonly IApiTokenService _tokenService;
    private readonly IUserRepository _userRepository;

    /// <summary>Creates the service.</summary>
    /// <param name="googleTokenValidator">The identity-provider boundary that validates the Google token.</param>
    /// <param name="tokenService">Issues and validates the API's own tokens.</param>
    /// <param name="userRepository">Persistence access for provisioning and lookup.</param>
    public AuthenticationService(
        IGoogleTokenValidator googleTokenValidator,
        IApiTokenService tokenService,
        IUserRepository userRepository)
    {
        _googleTokenValidator = googleTokenValidator ?? throw new ArgumentNullException(nameof(googleTokenValidator));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
    }

    /// <inheritdoc />
    public async Task<AuthenticationTokenResponse> SignInWithGoogleAsync(
        string? idToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idToken))
        {
            throw new HouseholdFinancesException(ErrorCode.InvalidInput, IdTokenIdentifier);
        }

        GoogleTokenPayload payload;
        try
        {
            payload = await _googleTokenValidator.ValidateAsync(idToken, cancellationToken);
        }
        catch (InvalidGoogleTokenException)
        {
            // Deliberately swallow the reason: the token is never logged, and the caller only needs
            // to know the token was rejected.
            throw new HouseholdFinancesException(ErrorCode.Unauthorized, GoogleTokenIdentifier);
        }

        var user = await _userRepository.GetByGoogleSubjectAsync(payload.Subject, cancellationToken);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                Name = ResolveDisplayName(payload),
                Email = ResolveEmail(payload),
                GoogleSubject = payload.Subject,
                // No local password: credentials are owned by Google.
                Password = null,
            };

            _userRepository.Add(user);
            await _userRepository.SaveChangesAsync(cancellationToken);
        }

        return IssueTokens(user);
    }

    /// <inheritdoc />
    public async Task<AuthenticationTokenResponse> RefreshAsync(
        string? refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new HouseholdFinancesException(ErrorCode.InvalidInput, RefreshTokenIdentifier);
        }

        Guid userId;
        try
        {
            userId = await _tokenService.ValidateRefreshTokenAsync(refreshToken, cancellationToken);
        }
        catch (InvalidRefreshTokenException)
        {
            throw new HouseholdFinancesException(ErrorCode.Unauthorized, RefreshTokenIdentifier);
        }

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken)
            // A valid refresh token for a deleted user is treated as an authentication failure, not
            // a not-found, so it cannot be used to probe for user existence.
            ?? throw new HouseholdFinancesException(ErrorCode.Unauthorized, RefreshTokenIdentifier);

        return IssueTokens(user);
    }

    private AuthenticationTokenResponse IssueTokens(User user)
    {
        var accessToken = _tokenService.CreateAccessToken(user);
        var refreshToken = _tokenService.CreateRefreshToken(user);

        return new AuthenticationTokenResponse(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            refreshToken.Token,
            refreshToken.ExpiresAtUtc,
            "Bearer");
    }

    private static string ResolveDisplayName(GoogleTokenPayload payload)
    {
        if (!string.IsNullOrWhiteSpace(payload.Name))
        {
            return payload.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(payload.Email))
        {
            var atIndex = payload.Email.IndexOf('@', StringComparison.Ordinal);
            var localPart = atIndex > 0 ? payload.Email[..atIndex] : payload.Email;
            if (!string.IsNullOrWhiteSpace(localPart))
            {
                return localPart.Trim();
            }
        }

        return "Google User";
    }

    private static string ResolveEmail(GoogleTokenPayload payload) =>
        !string.IsNullOrWhiteSpace(payload.Email)
            ? payload.Email.Trim()
            : $"{payload.Subject}@google.invalid";
}
