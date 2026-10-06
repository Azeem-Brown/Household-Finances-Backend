using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Domain.Entities;
using HouseholdFinances.Domain.Errors;
using HouseholdFinances.Domain.Models;
using HouseholdFinances.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Infrastructure.Services;

/// <summary>
/// Implements the Bill operations for a household the current, authenticated user belongs to. The
/// current user is resolved from <see cref="ICurrentUserService"/>; a household the user is not a
/// member of is reported as not found. A bill carries its household key directly, so a household's
/// bills are the rows tagged with that household. The specification omits a dedicated repository
/// for Bills, so this service uses <see cref="HouseholdFinancesDbContext"/> directly.
/// </summary>
public sealed class BillService : IBillService
{
    /// <summary>
    /// Identifier of significance reported when the bill's label fails validation. The error
    /// convention is <c>&lt;enum&gt; &lt;identifier&gt;</c>, so the identifier is the field being
    /// validated.
    /// </summary>
    public const string NameIdentifier = "Name";

    /// <summary>Identifier of significance reported when the bill's value fails validation.</summary>
    public const string ValueIdentifier = "Value";

    /// <summary>
    /// Identifier of significance reported when the date range fails validation. EndDate is the
    /// field that must not precede StartDate.
    /// </summary>
    public const string EndDateIdentifier = "EndDate";

    /// <summary>Identifier of significance reported when a recurring bill omits its interval.</summary>
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
    public BillService(HouseholdFinancesDbContext context, ICurrentUserService currentUserService)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
    }

    /// <inheritdoc />
    public async Task<BillDetail> CreateAsync(
        Guid householdId,
        BillInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var validated = Validate(input);
        var userId = ResolveCurrentUserId();
        await EnsureMemberAsync(householdId, userId, cancellationToken);

        var bill = new Bill
        {
            Id = Guid.NewGuid(),
            Name = validated.Name,
            Value = validated.Value,
            StartDate = validated.StartDate,
            EndDate = validated.EndDate,
            Recurring = validated.Recurring,
            Interval = validated.Interval,
            HouseholdId = householdId,
            UserId = userId
        };

        _context.Bills.Add(bill);
        await _context.SaveChangesAsync(cancellationToken);

        return ToDetail(bill);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<BillDetail>> ListAsync(
        Guid householdId,
        CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();
        await EnsureMemberAsync(householdId, userId, cancellationToken);

        var bills = await _context.Bills
            .Where(bill => bill.HouseholdId == householdId)
            .OrderBy(bill => bill.StartDate)
            .ThenBy(bill => bill.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return bills.Select(ToDetail).ToList();
    }

    /// <inheritdoc />
    public async Task<BillDetail> UpdateAsync(
        Guid householdId,
        Guid billId,
        BillInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var validated = Validate(input);
        var userId = ResolveCurrentUserId();
        await EnsureMemberAsync(householdId, userId, cancellationToken);

        var bill = await FindInHouseholdAsync(householdId, billId, cancellationToken)
            ?? throw new HouseholdFinancesException(ErrorCode.NotFound, billId);

        bill.Name = validated.Name;
        bill.Value = validated.Value;
        bill.StartDate = validated.StartDate;
        bill.EndDate = validated.EndDate;
        bill.Recurring = validated.Recurring;
        bill.Interval = validated.Interval;
        // HouseholdId and UserId are attribution and are intentionally left unchanged.

        await _context.SaveChangesAsync(cancellationToken);

        return ToDetail(bill);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(
        Guid householdId,
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        var userId = ResolveCurrentUserId();
        await EnsureMemberAsync(householdId, userId, cancellationToken);

        var bill = await FindInHouseholdAsync(householdId, billId, cancellationToken)
            ?? throw new HouseholdFinancesException(ErrorCode.NotFound, billId);

        _context.Bills.Remove(bill);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static ValidatedBill Validate(BillInput input)
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
        // carry no meaningful cadence, so the zero-valued Daily is stored, matching the Income
        // domain so the two stay consistent.
        return new ValidatedBill(
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
        // discloses the existence of another household's bills.
        if (!isMember)
        {
            throw new HouseholdFinancesException(ErrorCode.NotFound, householdId);
        }
    }

    private Task<Bill?> FindInHouseholdAsync(
        Guid householdId,
        Guid billId,
        CancellationToken cancellationToken) =>
        _context.Bills.FirstOrDefaultAsync(
            bill => bill.Id == billId && bill.HouseholdId == householdId,
            cancellationToken);

    private static BillDetail ToDetail(Bill bill) =>
        new(
            bill.Id,
            bill.Name,
            bill.Value,
            bill.StartDate,
            bill.EndDate,
            bill.Recurring,
            bill.Interval,
            bill.HouseholdId,
            bill.UserId);

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

    /// <summary>A bill input whose fields have passed validation and normalization.</summary>
    private sealed record ValidatedBill(
        string Name,
        double Value,
        DateTime StartDate,
        DateTime EndDate,
        bool Recurring,
        Interval Interval);
}
