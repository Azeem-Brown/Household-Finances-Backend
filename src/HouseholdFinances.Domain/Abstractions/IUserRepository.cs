using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Domain.Abstractions;

/// <summary>
/// Persistence access for the User domain. It reads and writes user records and the many-to-many
/// household membership. It never exposes the password to callers.
/// </summary>
public interface IUserRepository
{
    /// <summary>
    /// Finds the user with the supplied identifier, or <c>null</c> when no such user exists. The
    /// returned entity is tracked, so a change can be persisted with <see cref="SaveChangesAsync"/>.
    /// </summary>
    /// <param name="userId">The user's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching user, or <c>null</c>.</returns>
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the user provisioned from the supplied Google Identity subject (<c>sub</c>), or
    /// <c>null</c> when no user has been provisioned for it. Provisioning is keyed by subject, never
    /// by email, because an email address can change or be reassigned.
    /// </summary>
    /// <param name="googleSubject">The Google Identity subject identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching user, or <c>null</c>.</returns>
    Task<User?> GetByGoogleSubjectAsync(string googleSubject, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new user to the store. The change is not persisted until
    /// <see cref="SaveChangesAsync"/> is called.
    /// </summary>
    /// <param name="user">The user to add.</param>
    void Add(User user);

    /// <summary>
    /// Lists the households the user belongs to, ordered by name. Empty when the user has no
    /// household membership.
    /// </summary>
    /// <param name="userId">The user's identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user's households.</returns>
    Task<IReadOnlyList<Household>> GetHouseholdsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>Persists pending changes to the underlying store.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
