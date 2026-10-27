using HouseholdFinances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFinances.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="Item"/> to the specification "Items" table.
/// </summary>
public class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Items");

        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        builder.Property(item => item.Name).IsRequired();

        // Optional free-text description; null when the specification allows no description.
        builder.Property(item => item.Description);

        builder.Property(item => item.Price).HasColumnType(HouseholdFinancesDbContext.MoneyColumnType);
        builder.Property(item => item.HouseholdId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        builder.HasOne<Household>()
            .WithMany()
            .HasForeignKey(item => item.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
