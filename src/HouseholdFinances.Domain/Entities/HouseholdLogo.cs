namespace HouseholdFinances.Domain.Entities;

/// <summary>
/// The logo image for a household.
/// </summary>
/// <remarks>
/// The image lives in its own table rather than as columns on <see cref="Household"/>. The household
/// list and household detail reads never need the bytes, and those queries run on every page load,
/// so keeping the image separate stops the bytes travelling with them. The household identifier is
/// also the primary key, which is what makes a household have at most one logo.
/// </remarks>
public class HouseholdLogo
{
    /// <summary>
    /// The household the logo belongs to. Doubles as the primary key, so a household has at most one
    /// logo and a logo can never be shared between households.
    /// </summary>
    public Guid HouseholdId { get; set; }

    /// <summary>The image bytes exactly as uploaded; never transformed or re-encoded.</summary>
    public byte[] Content { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// The validated media type of <see cref="Content"/>. Only the media types the Household domain
    /// accepts are ever stored here.
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>When the logo was set or last replaced, in UTC.</summary>
    public DateTime UpdatedAtUtc { get; set; }
}
