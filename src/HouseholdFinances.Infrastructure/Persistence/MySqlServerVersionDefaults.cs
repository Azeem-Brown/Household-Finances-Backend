using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Infrastructure.Persistence;

/// <summary>
/// The MySQL server version used to configure the Pomelo provider.
/// A fixed version is used instead of <c>ServerVersion.AutoDetect</c> so that
/// building options, creating the model, and generating migrations never open a
/// database connection.
/// </summary>
internal static class MySqlServerVersionDefaults
{
    internal static readonly MySqlServerVersion ServerVersion = new(new Version(8, 0, 21));
}
