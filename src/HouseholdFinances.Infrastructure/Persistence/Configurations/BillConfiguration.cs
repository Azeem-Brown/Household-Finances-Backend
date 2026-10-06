using HouseholdFinances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFinances.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Bill"/> to the specification "Bills" table. A bill belongs to a
/// household and records the user who created it.
/// </summary>
public class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.ToTable("Bills");

        builder.HasKey(bill => bill.Id);
        builder.Property(bill => bill.Id).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        builder.Property(bill => bill.Name).IsRequired();
        builder.Property(bill => bill.Value).HasColumnType(HouseholdFinancesDbContext.MoneyColumnType);
        builder.Property(bill => bill.StartDate).IsRequired();
        builder.Property(bill => bill.EndDate).IsRequired();
        builder.Property(bill => bill.Recurring).IsRequired();
        builder.Property(bill => bill.Interval).IsRequired();
        builder.Property(bill => bill.HouseholdId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);
        builder.Property(bill => bill.UserId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(bill => bill.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict so deleting a user never silently removes household bills.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(bill => bill.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
