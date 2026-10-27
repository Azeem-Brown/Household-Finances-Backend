using HouseholdFinances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFinances.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Income"/> to the specification "Income" table. Income has no
/// household key; it reaches a household through the owning user's membership.
/// </summary>
public class IncomeConfiguration : IEntityTypeConfiguration<Income>
{
    public void Configure(EntityTypeBuilder<Income> builder)
    {
        builder.ToTable("Income");

        builder.HasKey(income => income.Id);
        builder.Property(income => income.Id).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        builder.Property(income => income.Name).IsRequired();
        builder.Property(income => income.Value).HasColumnType(HouseholdFinancesDbContext.MoneyColumnType);
        builder.Property(income => income.StartDate).IsRequired();
        builder.Property(income => income.EndDate).IsRequired();
        builder.Property(income => income.Recurring).IsRequired();
        // Interval is optional: a non-recurring entry stores NULL, a recurring entry stores its
        // cadence. The column is nullable so the two are distinguishable in storage.
        builder.Property(income => income.Interval);
        builder.Property(income => income.UserId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(income => income.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
