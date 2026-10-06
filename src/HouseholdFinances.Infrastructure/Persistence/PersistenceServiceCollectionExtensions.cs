using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFinances.Infrastructure.Persistence;

/// <summary>
/// Registration helpers for the EF Core persistence layer.
/// </summary>
public static class PersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Configuration key that holds the MySQL connection string. The value is not
    /// committed; supply it through user secrets, environment variables, or the
    /// host's own configuration sources.
    /// </summary>
    public const string ConnectionStringName = "DefaultConnection";

    /// <summary>
    /// Registers <see cref="HouseholdFinancesDbContext"/> using the MySQL
    /// connection string read from <paramref name="configuration"/>.
    /// </summary>
    public static IServiceCollection AddHouseholdFinancesDbContext(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{ConnectionStringName}' was not configured. Supply it through user secrets, environment variables, or appsettings.");
        }

        services.AddDbContext<HouseholdFinancesDbContext>(options =>
            options.UseMySql(connectionString, MySqlServerVersionDefaults.ServerVersion));

        return services;
    }
}
