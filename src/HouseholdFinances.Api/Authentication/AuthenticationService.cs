using HouseholdFinances.Api.Models;
using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Api.Authentication;

/// <summary>
/// Orchestrates Google Identity sign-in and API token issuance. Users are provisioned on first
/// successful validation, keyed by the Google subject, and no local password is ever created or
/// accepted. Refresh tokens are recorded server-side and rotated on use: a successful refresh
/// consumes the presented token and issues a fresh pair in the same family, and reuse of a consumed
/// token revokes the whole family.
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
    private readonly IRefreshTokenService _refreshTokenService;

    /// <summary>Creates the service.</summary>
    /// <param name="googleTokenValidator">The identity-provider boundary that validates the Google token.</param>
    /// <param name="tokenService">Issues and validates the API's own tokens.</param>
    /// <param name="userRepository">Persistence access for provisioning and lookup.</param>
    /// <param name="refreshTokenService">Records, consumes, and revokes refresh tokens.</param>
    public AuthenticationService(
        IGoogleTokenValidator googleTokenValidator,
        IApiTokenService tokenService,
        IUserRepository userRepository,
        IRefreshTokenService refreshTokenService)
    {
        _googleTokenValidator = googleTokenValidator ?? throw new ArgumentNullException(nameof(googleTokenValidator));
        _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _refreshTokenService = refreshTokenService ?? throw new ArgumentNullException(nameof(refreshTokenService));
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

        // A sign-in starts a brand-new token family.
        return await IssueTokensAsync(user, Guid.NewGuid(), cancellationToken);
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

        ValidatedRefreshToken validated;
        try
        {
            validated = await _tokenService.ValidateRefreshTokenAsync(refreshToken, cancellationToken);
        }
        catch (InvalidRefreshTokenException)
        {
            throw new HouseholdFinancesException(ErrorCode.Unauthorized, RefreshTokenIdentifier);
        }

        // Atomically consume the presented token. Reuse of a consumed token, or a token from a
        // revoked family, revokes the whole family and is reported as unauthorized.
        var consume = await _refreshTokenService.ConsumeAsync(validated.UserId, validated.Jti, cancellationToken);

        if (consume.Outcome == RefreshTokenConsumeOutcome.NotFound)
        {
            throw new HouseholdFinancesException(ErrorCode.Unauthorized, RefreshTokenIdentifier);
        }

        if (consume.Outcome == RefreshTokenConsumeOutcome.Reused)
        {
            await _refreshTokenService.RevokeFamilyAsync(consume.FamilyId!.Value, cancellationToken);
            throw new HouseholdFinancesException(ErrorCode.Unauthorized, RefreshTokenIdentifier);
        }

        var user = await _userRepository.GetByIdAsync(validated.UserId, cancellationToken)
            // A valid refresh token for a deleted user is treated as an authentication failure, not
            // a not-found, so it cannot be used to probe for user existence.
            ?? throw new HouseholdFinancesException(ErrorCode.Unauthorized, RefreshTokenIdentifier);

        // The new pair belongs to the same family as the token that was just consumed.
        return await IssueTokensAsync(user, consume.FamilyId!.Value, cancellationToken);
    }

    private async Task<AuthenticationTokenResponse> IssueTokensAsync(
        User user,
        Guid familyId,
        CancellationToken cancellationToken)
    {
        var accessToken = _tokenService.CreateAccessToken(user);
        var refreshToken = _tokenService.CreateRefreshToken(user);

        await _refreshTokenService.RecordIssuedAsync(
            new RefreshTokenIssue(
                user.Id,
                refreshToken.Jti,
                familyId,
                DateTime.UtcNow,
                refreshToken.ExpiresAtUtc),
            cancellationToken);

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
