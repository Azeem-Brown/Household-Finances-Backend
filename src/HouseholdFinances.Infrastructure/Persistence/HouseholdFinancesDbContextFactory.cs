using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HouseholdFinances.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build the context without the API
/// host. The connection string is read from configuration (appsettings,
/// environment variables, or user secrets) and falls back to a credential-free
/// local placeholder, so generating migrations does not require a running database.
/// </summary>
public class HouseholdFinancesDbContextFactory : IDesignTimeDbContextFactory<HouseholdFinancesDbContext>
{
    private const string FallbackConnectionString = "Server=localhost;Database=household_finances;";

    public HouseholdFinancesDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .AddUserSecrets<HouseholdFinancesDbContext>(optional: true)
            .Build();

        var connectionString = configuration.GetConnectionString(
            PersistenceServiceCollectionExtensions.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = FallbackConnectionString;
        }

        var optionsBuilder = new DbContextOptionsBuilder<HouseholdFinancesDbContext>();
        optionsBuilder.UseMySql(connectionString, MySqlServerVersionDefaults.ServerVersion);

        return new HouseholdFinancesDbContext(optionsBuilder.Options);
    }
}
