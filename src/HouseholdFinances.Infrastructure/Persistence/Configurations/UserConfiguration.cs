using HouseholdFinances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFinances.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="User"/> to the specification "Users" table.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);
        builder.Property(user => user.Id).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        builder.Property(user => user.Name).IsRequired();
        builder.Property(user => user.Email).IsRequired();

        // Retained for specification compatibility; null until third-party
        // authentication is wired up.
        builder.Property(user => user.Password);
    }
}
