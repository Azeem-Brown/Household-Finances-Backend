using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HouseholdFinances.Infrastructure.Persistence;

/// <summary>
/// Reports migrations that exist in the code but have not been applied to the database.
/// </summary>
/// <remarks>
/// Migrations are applied by hand, so the schema can drift behind the code after a merge. Without
/// this check the drift surfaces as a generic 500 on whichever endpoint first touches a missing
/// table, which hides the real cause. The check only reports: it never applies a migration and
/// never blocks startup.
/// </remarks>
public static class PendingMigrationWarning
{
    /// <summary>The message logged when one or more migrations are pending.</summary>
    public const string PendingTemplate =
        "The database schema is behind the code. Pending migrations: {PendingMigrations}. "
        + "Run 'dotnet ef database update --project src/HouseholdFinances.Infrastructure "
        + "--startup-project src/HouseholdFinances.Api' before using the affected endpoints.";

    /// <summary>
    /// Reads the migration identifiers that exist in the code but are not applied to the database.
    /// </summary>
    /// <param name="context">The context whose database is inspected.</param>
    /// <returns>The pending identifiers, oldest first; empty when the schema is current.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    public static IReadOnlyList<string> GetPending(HouseholdFinancesDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Database.GetPendingMigrations().ToList();
    }

    /// <summary>
    /// Logs a warning naming every pending migration, or nothing at all when none are pending.
    /// </summary>
    /// <param name="pendingMigrations">The identifiers returned by <see cref="GetPending"/>.</param>
    /// <param name="logger">The logger to write to.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="pendingMigrations"/> or <paramref name="logger"/> is null.
    /// </exception>
    public static void LogIfPending(IReadOnlyList<string> pendingMigrations, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(pendingMigrations);
        ArgumentNullException.ThrowIfNull(logger);

        if (pendingMigrations.Count == 0)
        {
            return;
        }

        logger.LogWarning(PendingTemplate, string.Join(", ", pendingMigrations));
    }
}
