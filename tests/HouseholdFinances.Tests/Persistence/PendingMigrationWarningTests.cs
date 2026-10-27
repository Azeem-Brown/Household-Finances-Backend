using HouseholdFinances.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace HouseholdFinances.Tests.Persistence;

/// <summary>
/// Verifies the pending-migration warning: it names every pending migration at warning level, and
/// stays silent when the schema is current.
/// </summary>
public class PendingMigrationWarningTests
{
    [Fact]
    public void LogIfPending_WarnsAndNamesEveryPendingMigration()
    {
        CapturingLogger logger = new CapturingLogger();

        PendingMigrationWarning.LogIfPending(
            new[] { "20261027131241_AddHouseholdLogo", "20261028090000_AddSomething" },
            logger);

        (LogLevel level, string message) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, level);
        Assert.Contains("20261027131241_AddHouseholdLogo", message, StringComparison.Ordinal);
        Assert.Contains("20261028090000_AddSomething", message, StringComparison.Ordinal);
        Assert.Contains("dotnet ef database update", message, StringComparison.Ordinal);
    }

    [Fact]
    public void LogIfPending_LogsNothingWhenEveryMigrationIsApplied()
    {
        CapturingLogger logger = new CapturingLogger();

        PendingMigrationWarning.LogIfPending(Array.Empty<string>(), logger);

        Assert.Empty(logger.Entries);
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
