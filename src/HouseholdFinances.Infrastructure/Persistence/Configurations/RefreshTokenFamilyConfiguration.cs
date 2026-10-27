using HouseholdFinances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFinances.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="RefreshTokenFamily"/> to the "RefreshTokenFamilies" table. A family belongs to a
/// single user and carries the revocation flag that invalidates every token in the chain.
/// </summary>
public class RefreshTokenFamilyConfiguration : IEntityTypeConfiguration<RefreshTokenFamily>
{
    public void Configure(EntityTypeBuilder<RefreshTokenFamily> builder)
    {
        builder.ToTable("RefreshTokenFamilies");

        builder.HasKey(family => family.Id);
        builder.Property(family => family.Id).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);
        builder.Property(family => family.UserId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);
        builder.Property(family => family.Revoked).IsRequired();

        // Deleting a user removes their token families; the tokens cascade off the family.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(family => family.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(family => family.UserId);
    }
}
