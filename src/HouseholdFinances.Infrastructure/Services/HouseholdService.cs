using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;

namespace HouseholdFinances.Infrastructure.Services;

/// <summary>
/// Implements the Household operations for the current, authenticated user. The user is always
/// resolved from <see cref="ICurrentUserService"/>, so a client can never target another user's
/// household. Incomes and Payments are computed on read as the sum of the entry occurrences inside
/// a requested calendar window, and are never stored authoritatively. The clock comes from
/// <see cref="TimeProvider"/>, which the tests pin. The entry counts are the number of stored
/// entries and deliberately ignore the window, so a household whose entries fall outside the
/// requested period still reports that it has entries.
/// </summary>
public sealed class HouseholdService : IHouseholdService
{
    /// <summary>
    /// Identifier of significance reported when the household name fails validation. The error
    /// convention is <c>&lt;enum&gt; &lt;identifier&gt;</c>, so the identifier is the field being
    /// validated.
    /// </summary>
    public const string NameIdentifier = "Name";

    /// <summary>
    /// Identifier of significance reported when the current user cannot be resolved from the
    /// authenticated principal. A fixed token is used so no user-supplied value reaches the response
    /// or logs.
    /// </summary>
    public const string CurrentUserIdentifier = "me";

    private readonly IHouseholdRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly TimeProvider _timeProvider;

    /// <summary>Creates the service.</summary>
    /// <param name="repository">Persistence access for the Household domain.</param>
    /// <param name="currentUserService">Resolves the authenticated user for the current request.</param>
    /// <param name="timeProvider">Supplies the UTC clock used to derive the calendar window.</param>
    public HouseholdService(
        IHouseholdRepository repository,
        ICurrentUserService currentUserService,
        TimeProvider timeProvider)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <inheritdoc />
    public async Task<HouseholdDetail> CreateAsync(string? name, CancellationToken cancellationToken = default)
    {
        var trimmedName = name?.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            throw new HouseholdFinancesException(ErrorCode.InvalidInput, NameIdentifier);
        }

        var userId = ResolveCurrentUserId();

        var household = new Household
        {
            Id = Guid.NewGuid(),
            Name = trimmedName
        };

        // Enforce the one-membership-per-user/household invariant in the application layer. This
        // flow always allocates a fresh household id, so the check is defensive; the unique index on
        // the join table backs it at the database.
        if (await _repository.MembershipExistsAsync(household.Id, userId, cancellationToken))
        {
            throw new HouseholdFinancesException(ErrorCode.Conflict, household.Id);
        }

        // Incomes and Payments are intentionally not written; they are computed on read.
        await _repository.AddAsync(household, cancellationToken);
        await _repository.AddMembershipAsync(
            new UserHousehold
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                HouseholdId = household.Id
            },
            cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        // A new household has no entries. The default Monthly window keeps the read model shape
        // consistent without adding a period parameter to the create flow.
        var totals = await _repository.GetTotalsAsync(
            household.Id,
            CurrentWindow(Interval.Monthly),
            cancellationToken);
        var entryCounts = await _repository.GetEntryCountsAsync(household.Id, cancellationToken);

        return ToDetail(household, totals, entryCounts);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HouseholdDetail>> ListForCurrentUserAsync(
        Interval period,
        CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();

        // Resolve "now" once per request so every household in the list uses one window.
        var window = CurrentWindow(period);

        var households = await _repository.GetByMemberAsync(userId, cancellationToken);

        var details = new List<HouseholdDetail>(households.Count);
        foreach (var household in households)
        {
            var totals = await _repository.GetTotalsAsync(household.Id, window, cancellationToken);
            var entryCounts = await _repository.GetEntryCountsAsync(household.Id, cancellationToken);
            details.Add(ToDetail(household, totals, entryCounts));
        }

        return details;
    }

    /// <inheritdoc />
    public async Task<HouseholdDetail> GetByIdAsync(
        Guid householdId,
        Interval period,
        CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();

        // A household the user does not belong to is reported as not found so the API never
        // discloses the existence of another user's household.
        var household = await _repository.GetForMemberAsync(householdId, userId, cancellationToken)
            ?? throw new HouseholdFinancesException(ErrorCode.NotFound, householdId);

        var totals = await _repository.GetTotalsAsync(
            householdId,
            CurrentWindow(period),
            cancellationToken);

        // Deliberately read without the window: the counts describe the household's entries, not
        // the occurrences inside the requested period.
        var entryCounts = await _repository.GetEntryCountsAsync(householdId, cancellationToken);

        return ToDetail(household, totals, entryCounts);
    }

    private PeriodWindow CurrentWindow(Interval period) =>
        PeriodWindow.For(period, DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime));

    private static HouseholdDetail ToDetail(
        Household household,
        HouseholdTotals totals,
        HouseholdEntryCounts entryCounts) =>
        new(
            household.Id,
            household.Name,
            RoundMoney(totals.Incomes),
            RoundMoney(totals.Payments),
            entryCounts.Incomes,
            entryCounts.Bills);

    // Money is USD with two decimal places, half-up (recorded decision).
    private static double RoundMoney(double value) =>
        Math.Round(value, 2, MidpointRounding.AwayFromZero);

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
