namespace HouseholdFinances.Tests;

/// <summary>
/// A <see cref="TimeProvider"/> pinned to one UTC instant so calendar-window tests are
/// deterministic regardless of when they run.
/// </summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _utcNow;

    /// <summary>Creates a provider that always reports <paramref name="utcNow"/>.</summary>
    /// <param name="utcNow">The UTC instant to report.</param>
    public FixedTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _utcNow;
}
