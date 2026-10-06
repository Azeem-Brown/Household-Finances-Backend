using HouseholdFinances.Domain.Abstractions;
using HouseholdFinances.Infrastructure.Repositories;
using HouseholdFinances.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFinances.Infrastructure.DependencyInjection;

/// <summary>
/// Registration helpers for the Infrastructure implementations of the Domain abstractions.
/// </summary>
public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Household domain's repository and service implementations. Both are scoped so
    /// they share the request's <see cref="Persistence.HouseholdFinancesDbContext"/>.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection, so calls can be chained.</returns>
    public static IServiceCollection AddHouseholdFinancesHouseholdDomain(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IHouseholdRepository, HouseholdRepository>();
        services.AddScoped<IHouseholdService, HouseholdService>();

        return services;
    }
}
