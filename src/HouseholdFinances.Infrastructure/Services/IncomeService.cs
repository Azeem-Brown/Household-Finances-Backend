using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;
using HouseholdFinances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Infrastructure.Services;

/// <summary>
/// Implements the Income operations for a household the current, authenticated user belongs to. The
/// current user is resolved from <see cref="ICurrentUserService"/>; a household the user is not a
/// member of is reported as not found. The specification omits a dedicated repository for Income, so
/// this service uses <see cref="HouseholdFinancesDbContext"/> directly.
/// </summary>
public sealed class IncomeService : IIncomeService
{
    /// <summary>
    /// Identifier of significance reported when the entry's label fails validation. The error
    /// convention is <c>&lt;enum&gt; &lt;identifier&gt;</c>, so the identifier is the field being
    /// validated.
    /// </summary>
    public const string NameIdentifier = "Name";

    /// <summary>Identifier of significance reported when the entry's value fails validation.</summary>
    public const string ValueIdentifier = "Value";

    /// <summary>
    /// Identifier of significance reported when the date range fails validation. EndDate is the
    /// field that must not precede StartDate.
    /// </summary>
    public const string EndDateIdentifier = "EndDate";

    /// <summary>Identifier of significance reported when a recurring entry omits its interval.</summary>
    public const string IntervalIdentifier = "Interval";

    /// <summary>
    /// Identifier of significance reported when the current user cannot be resolved from the
    /// authenticated principal. A fixed token is used so no user-supplied value reaches the response
    /// or logs.
    /// </summary>
    public const string CurrentUserIdentifier = "me";

    private readonly HouseholdFinancesDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>Creates the service.</summary>
    /// <param name="context">The EF Core context for the household finances schema.</param>
    /// <param name="currentUserService">Resolves the authenticated user for the current request.</param>
    public IncomeService(HouseholdFinancesDbContext context, ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc />
    public async Task<IncomeDetail> CreateAsync(
        Guid householdId,
        IncomeInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var validated = Validate(input);
        var userId = ResolveCurrentUserId();
        await EnsureMemberAsync(householdId, userId, cancellationToken);

        var income = new Income
        {
            Id = Guid.NewGuid(),
            Name = validated.Name,
            Value = validated.Value,
            StartDate = validated.StartDate,
            EndDate = validated.EndDate,
            Recurring = validated.Recurring,
            Interval = validated.Interval,
            UserId = userId
        };

        _context.Incomes.Add(income);
        await _context.SaveChangesAsync(cancellationToken);

        return ToDetail(income);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IncomeDetail>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();
        await EnsureMemberAsync(householdId, userId, cancellationToken);

        var memberIds = MemberIdsQuery(householdId);

        var incomes = await _context.Incomes
            .Where(income => memberIds.Contains(income.UserId))
            .OrderBy(income => income.StartDate)
            .ThenBy(income => income.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return incomes.Select(ToDetail).ToList();
    }

    /// <inheritdoc />
    public async Task<IncomeDetail> UpdateAsync(
        Guid householdId,
        Guid incomeId,
        IncomeInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var validated = Validate(input);
        var userId = ResolveCurrentUserId();
        await EnsureMemberAsync(householdId, userId, cancellationToken);

        var income = await FindInHouseholdAsync(householdId, incomeId, cancellationToken)
            ?? throw new HouseholdFinancesException(ErrorCode.NotFound, incomeId);

        income.Name = validated.Name;
        income.Value = validated.Value;
        income.StartDate = validated.StartDate;
        income.EndDate = validated.EndDate;
        income.Recurring = validated.Recurring;
        income.Interval = validated.Interval;
        // UserId is attribution and is intentionally left unchanged.

        await _context.SaveChangesAsync(cancellationToken);

        return ToDetail(income);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid householdId,
        Guid incomeId,
        CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();
        await EnsureMemberAsync(householdId, userId, cancellationToken);

        var income = await FindInHouseholdAsync(householdId, incomeId, cancellationToken)
            ?? throw new HouseholdFinancesException(ErrorCode.NotFound, incomeId);

        _context.Incomes.Remove(income);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static ValidatedIncome Validate(IncomeInput input)
    {
        var trimmedName = input.Name?.Trim();
        if (string.IsNullOrEmpty(trimmedName))
        {
            throw new HouseholdFinancesException(ErrorCode.InvalidInput, NameIdentifier);
        }

        if (input.Value < 0)
        {
            throw new HouseholdFinancesException(ErrorCode.InvalidInput, ValueIdentifier);
        }

        if (input.StartDate > input.EndDate)
        {
            throw new HouseholdFinancesException(ErrorCode.InvalidInput, EndDateIdentifier);
        }

        if (input.Recurring && input.Interval is null)
        {
            throw new HouseholdFinancesException(ErrorCode.InvalidInput, IntervalIdentifier);
        }

        // Money is USD with two decimal places, half-up (recorded decision). Non-recurring entries
        // carry no meaningful cadence, so the zero-valued Daily is stored.
        return new ValidatedIncome(
            trimmedName,
            RoundMoney(input.Value),
            input.StartDate,
            input.EndDate,
            input.Recurring,
            input.Interval ?? Interval.Daily);
    }

    private async Task EnsureMemberAsync(Guid householdId, Guid userId, CancellationToken cancellationToken)
    {
        var isMember = await _context.UserHouseholds.AnyAsync(
            membership => membership.HouseholdId == householdId && membership.UserId == userId,
            cancellationToken);

        // A household the user is not a member of is reported as not found, so the API never
        // discloses the existence of another household's income.
        if (!isMember)
        {
            throw new HouseholdFinancesException(ErrorCode.NotFound, householdId);
        }
    }

    private Task<Income?> FindInHouseholdAsync(
        Guid householdId,
        Guid incomeId,
        CancellationToken cancellationToken)
    {
        var memberIds = MemberIdsQuery(householdId);

        return _context.Incomes.FirstOrDefaultAsync(
            income => income.Id == incomeId && memberIds.Contains(income.UserId),
            cancellationToken);
    }

    private IQueryable<Guid> MemberIdsQuery(Guid householdId) =>
        _context.UserHouseholds
            .Where(membership => membership.HouseholdId == householdId)
            .Select(membership => membership.UserId);

    private static IncomeDetail ToDetail(Income income) =>
        new(
            income.Id,
            income.Name,
            income.Value,
            income.StartDate,
            income.EndDate,
            income.Recurring,
            income.Interval,
            income.UserId);

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

    /// <summary>An income input whose fields have passed validation and normalization.</summary>
    private sealed record ValidatedIncome(
        string Name,
        double Value,
        DateTime StartDate,
        DateTime EndDate,
        bool Recurring,
        Interval Interval);
}
