namespace HouseholdFinances.Domain.Models;

/// <summary>
/// A household's logo as handed to the API layer: the raw image bytes and the validated media type
/// used to serve them back.
/// </summary>
/// <param name="Content">The image bytes.</param>
/// <param name="ContentType">The validated media type of the image.</param>
public sealed record HouseholdLogoContent(byte[] Content, string ContentType);
