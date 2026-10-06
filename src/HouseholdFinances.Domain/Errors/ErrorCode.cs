namespace HouseholdFinances.Domain.Errors;

/// <summary>
/// Backend failure categories. Each member carries an explicit, stable numeric value so the
/// code that travels over the wire never changes as members are added. The values match the
/// sibling frontend's client-side categories (frontend issue #7) so an API response can be
/// mapped onto them without translation.
/// </summary>
public enum ErrorCode
{
    /// <summary>An unexpected or otherwise unclassified failure. Numeric value 0.</summary>
    Unknown = 0,

    /// <summary>The requested entity or record could not be located. Numeric value 1.</summary>
    NotFound = 1,

    /// <summary>The supplied input failed validation. Numeric value 2.</summary>
    InvalidInput = 2,

    /// <summary>The current user is not authenticated for the requested action. Numeric value 3.</summary>
    Unauthorized = 3,

    /// <summary>The requested change conflicts with the current state. Numeric value 4.</summary>
    Conflict = 4
}
