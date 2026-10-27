namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// Join row linking a user to a household. Matches the specification "User Household" schema.
/// </summary>
public class UserHousehold
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid HouseholdId { get; set; }
}
