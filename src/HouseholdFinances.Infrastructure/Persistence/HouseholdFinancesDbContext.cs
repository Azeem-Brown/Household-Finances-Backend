using HouseholdFinances.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFinances.Infrastructure.Persistence;

/// <summary>
/// EF Core context for the household finances schema. Exposes the seven
/// specification entities (Household, Users, UserHousehold, Income, Bills,
/// Items, Goals) and applies their fluent configurations.
/// </summary>
public class HouseholdFinancesDbContext : DbContext
{
    /// <summary>
    /// Column type used for money values. The specification models money as
    /// <see cref="double"/>, so the MySQL <c>double</c> column type is used.
    /// </summary>
    public const string MoneyColumnType = "double";

    /// <summary>
    /// Column type used for GUID identifiers, matching the recorded decision to
    /// store them as <c>char(36)</c>.
    /// </summary>
    public const string GuidColumnType = "char(36)";

    public HouseholdFinancesDbContext(DbContextOptions<HouseholdFinancesDbContext> options)
        : base(options)
    {
    }

    public DbSet<Household> Households => Set<Household>();

    public DbSet<User> Users => Set<User>();

    public DbSet<UserHousehold> UserHouseholds => Set<UserHousehold>();

    public DbSet<Income> Incomes => Set<Income>();

    public DbSet<Bill> Bills => Set<Bill>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<Goal> Goals => Set<Goal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HouseholdFinancesDbContext).Assembly);
    }
}
