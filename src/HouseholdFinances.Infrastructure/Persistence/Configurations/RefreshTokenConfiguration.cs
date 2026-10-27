using HouseholdFinances.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFinances.Infrastructure.Persistence.Configurations;

/// <summary>
/// Maps <see cref="RefreshToken"/> to the "RefreshTokens" table. Each row records one issued token,
/// keyed by its unique <c>jti</c>. The consumed flag is a concurrency token so a conditional update
/// on it makes the first use win and a second, near-simultaneous use fail as reuse.
/// </summary>
public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);
        builder.Property(token => token.UserId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);
        builder.Property(token => token.FamilyId).HasColumnType(HouseholdFinancesDbContext.GuidColumnType);

        // The jti is a GUID string; a bounded length keeps the unique index on a varchar rather than
        // an unbounded longtext column, which MySQL rejects for a unique index.
        builder.Property(token => token.Jti).IsRequired().HasMaxLength(64);
        builder.Property(token => token.IssuedAtUtc).IsRequired();
        builder.Property(token => token.ExpiresAtUtc).IsRequired();

        // Consumed is the conditional-update flag. Marking it a concurrency token makes EF append
        // "AND Consumed = <original>" to the update, so two refreshes cannot both consume a row.
        builder.Property(token => token.Consumed).IsRequired().IsConcurrencyToken();

        builder.HasOne<RefreshTokenFamily>()
            .WithMany()
            .HasForeignKey(token => token.FamilyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(token => token.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Each token is recorded once and matched on its jti.
        builder.HasIndex(token => token.Jti).IsUnique();

        // Supports revoking a whole family and the opportunistic cleanup of expired rows.
        builder.HasIndex(token => token.FamilyId);
        builder.HasIndex(token => token.ExpiresAtUtc);
    }
}
