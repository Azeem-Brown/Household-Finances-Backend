using HouseholdFinances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFinances.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="HouseholdLogo"/> to the "HouseholdLogo" table. The household identifier is both
/// the primary key and the foreign key, which enforces one logo per household.
/// </summary>
public class HouseholdLogoConfiguration : IEntityTypeConfiguration<HouseholdLogo>
{
    public void Configure(EntityTypeBuilder<HouseholdLogo> builder)
    {
        builder.ToTable("HouseholdLogo");

        builder.HasKey(logo => logo.HouseholdId);
        builder.Property(logo => logo.HouseholdId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        // mediumblob holds the enforced 2 MB ceiling with generous headroom, so a rejected oversize
        // upload is never truncated on the way in. The service is what enforces the limit.
        builder.Property(logo => logo.Content).IsRequired().HasColumnType("mediumblob");

        builder.Property(logo => logo.ContentType).IsRequired().HasMaxLength(64);

        builder.Property(logo => logo.UpdatedAtUtc).IsRequired();

        // A relation without a navigation on Household, so the household reads keep loading only the
        // household columns and never the image.
        builder.HasOne<Household>()
            .WithOne()
            .HasForeignKey<HouseholdLogo>(logo => logo.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
