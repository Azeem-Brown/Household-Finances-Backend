using HouseholdFinances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFinances.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Household"/> to the specification "Household" table.
/// </summary>
public class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("Household");

        builder.HasKey(household => household.Id);
        builder.Property(household => household.Id).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        builder.Property(household => household.Name).IsRequired();

        builder.Property(household => household.Incomes).HasColumnType(HouseholdFinancesDbContext.MoneyColumnType);
        builder.Property(household => household.Payments).HasColumnType(HouseholdFinancesDbContext.MoneyColumnType);
    }
}
