using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace HouseholdFinances.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can build the context without the API
/// host. The connection string is read from configuration (appsettings,
/// user secrets, or environment variables) and falls back to a credential-free
/// local placeholder, so generating migrations does not require a running database.
/// </summary>
/// <remarks>
/// User secrets are resolved from this assembly's <c>UserSecretsId</c>, which is
/// deliberately the same value as the API project's. That lets
/// <c>dotnet user-secrets set "ConnectionStrings:DefaultConnection" ... --project src/HouseholdFinances.Api</c>
/// configure the <c>dotnet ef</c> commands as well as the running host from a single store.
/// </remarks>
public class HouseholdFinancesDbContextFactory : IDesignTimeDbContextFactory<HouseholdFinancesDbContext>
{
    private const string FallbackConnectionString = "Server=localhost;Database=household_finances;";

    public HouseholdFinancesDbContext CreateDbContext(string[] args)
    {
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            // Same order as the ASP.NET Core host: user secrets first, then environment
            // variables, so a ConnectionStrings__DefaultConnection environment variable wins.
            .AddUserSecrets<HouseholdFinancesDbContext>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        string? connectionString = configuration.GetConnectionString(
            PersistenceServiceCollectionExtensions.ConnectionStringName);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = FallbackConnectionString;
        }

        DbContextOptionsBuilder<HouseholdFinancesDbContext> optionsBuilder = new DbContextOptionsBuilder<HouseholdFinancesDbContext>();
        optionsBuilder.UseMySql(connectionString, MySqlServerVersionDefaults.ServerVersion);

        return new HouseholdFinancesDbContext(optionsBuilder.Options);
    }
}
