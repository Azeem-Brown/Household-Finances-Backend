using HouseholdFinances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFinances.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="UserHousehold"/> join row along with its user and household
/// keys, resolving the many-to-many household membership.
/// </summary>
public class UserHouseholdConfiguration : IEntityTypeConfiguration<UserHousehold>
{
    public void Configure(EntityTypeBuilder<UserHousehold> builder)
    {
        builder.ToTable("UserHousehold");

        builder.HasKey(membership => membership.Id);
        builder.Property(membership => membership.Id).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);
        builder.Property(membership => membership.UserId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);
        builder.Property(membership => membership.HouseholdId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(membership => membership.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
