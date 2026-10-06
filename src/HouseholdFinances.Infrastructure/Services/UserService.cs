using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Infrastructure.Services;

/// <summary>
/// Implements the User profile operations. The current user is resolved from
/// <see cref="ICurrentUserService"/>, so a client can never target another user's profile.
/// </summary>
public sealed class UserService : IUserService
{
    /// <summary>
    /// Identifier of significance reported when the display name fails validation. The error
    /// convention is <c>&lt;enum&gt; &lt;identifier&gt;</c>, so the identifier is the field being
    /// validated.
    /// </summary>
    public const string NameIdentifier = "Name";

    /// <summary>
    /// Identifier of significance reported when the current user cannot be resolved from the
    /// authenticated principal. A fixed token is used so no user-supplied value reaches the
    /// response or logs.
    /// </summary>
    public const string CurrentUserIdentifier = "me";

    private readonly IUserRepository _repository;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>Creates the service.</summary>
    /// <param name="repository">Persistence access for the User domain.</param>
    /// <param name="currentUserService">Resolves the authenticated user for the current request.</param>
    public UserService(IUserRepository repository, ICurrentUserService currentUserService)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc />
    public async Task<UserProfile> GetCurrentUserProfileAsync(CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();

        var user = await _repository.GetByIdAsync(userId, cancellationToken)
            ?? throw new HouseholdFinancesException(ErrorCode.NotFound, userId);

        return await BuildProfileAsync(user, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<UserProfile> UpdateCurrentUserDisplayNameAsync(
        string? name,
        CancellationToken cancellationToken = default)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            throw new HouseholdFinancesException(ErrorCode.InvalidInput, NameIdentifier);
        }

        var userId = ResolveCurrentUserId();

        var user = await _repository.GetByIdAsync(userId, cancellationToken)
            ?? throw new HouseholdFinancesException(ErrorCode.NotFound, userId);

        user.Name = trimmedName;
        await _repository.SaveChangesAsync(cancellationToken);

        return await BuildProfileAsync(user, cancellationToken);
    }

    private async Task<UserProfile> BuildProfileAsync(User user, CancellationToken cancellationToken)
    {
        var households = await _repository.GetHouseholdsAsync(user.Id, cancellationToken);

        var summaries = households
            .Select(household => new HouseholdSummary(household.Id, household.Name))
            .ToList();

        // The profile record has no password member, so the hash can never be serialized.
        return new UserProfile(user.Id, user.Name, user.Email, summaries);
    }

    private Guid ResolveCurrentUserId()
    {
        var identifier = _currentUserService.UserIdentifier;

        if (!_currentUserService.IsAuthenticated
            || string.IsNullOrWhiteSpace(identifier)
            || !Guid.TryParse(identifier, out var userId))
        {
            // Defense in depth: the default authorization policy already rejects unauthenticated
            // requests with 401 before the action runs.
            throw new HouseholdFinancesException(ErrorCode.Unauthorized, CurrentUserIdentifier);
        }

        return userId;
    }
}
