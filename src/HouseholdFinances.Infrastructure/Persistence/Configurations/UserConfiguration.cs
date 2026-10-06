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

        // The Google Identity subject a user is provisioned from. Bounded length (Google's sub is
        // at most 255 characters) so the unique index provisions as varchar(255) rather than
        // longtext: MySQL rejects a unique index on an unbounded longtext column. Nullable, because
        // rows created before Google sign-in was wired up have no subject, and MySQL permits many
        // NULLs in a unique index.
        builder.Property(user => user.GoogleSubject).HasMaxLength(255);
        builder.HasIndex(user => user.GoogleSubject).IsUnique();

        // Retained for specification compatibility; null until third-party
        // authentication is wired up.
        builder.Property(user => user.Password);
    }
}
