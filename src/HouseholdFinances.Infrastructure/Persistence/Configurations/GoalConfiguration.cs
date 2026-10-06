using HouseholdFinances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFinances.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Goal"/> to the specification "Goals" table. A goal belongs to a
/// household and records the user who created it.
/// </summary>
public class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.ToTable("Goals");

        builder.HasKey(goal => goal.Id);
        builder.Property(goal => goal.Id).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        builder.Property(goal => goal.Name).IsRequired();
        builder.Property(goal => goal.Value).HasColumnType(HouseholdFinancesDbContext.MoneyColumnType);
        builder.Property(goal => goal.StartDate).IsRequired();
        builder.Property(goal => goal.EndDate).IsRequired();
        builder.Property(goal => goal.Recurring).IsRequired();
        builder.Property(goal => goal.Interval).IsRequired();
        builder.Property(goal => goal.HouseholdId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);
        builder.Property(goal => goal.UserId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);
        builder.Property(goal => goal.Total).HasColumnType(HouseholdFinancesDbContext.MoneyColumnType);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(goal => goal.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict so deleting a user never silently removes household goals.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(goal => goal.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
