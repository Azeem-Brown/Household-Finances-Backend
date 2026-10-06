using System.Globalization;

namespace HouseholdFinances.Domain.Errors;

/// <summary>
/// Formats errors using the specification's Error Handling convention: the error enum name
/// followed by the identifier or number of significance that was being reached (for example,
/// <c>NotFound 42</c>). This mirrors the sibling frontend's <c>ClientErrorFormatter</c> so both
/// repositories produce the same message shape.
/// </summary>
public static class ErrorFormatter
{
    /// <summary>
    /// Builds the display string for an error as <c>&lt;enum&gt; &lt;identifier&gt;</c>.
    /// </summary>
    /// <param name="errorCode">The backend error category.</param>
    /// <param name="identifier">The identifier or number of significance that was being reached.</param>
    /// <returns>The formatted display string, with the enum placed before the identifier.</returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identifier"/> is <see langword="null"/>.
    /// </exception>
    public static string Format(ErrorCode errorCode, object identifier)
    {
        ArgumentNullException.ThrowIfNull(identifier);

        var identifierText = Convert.ToString(identifier, CultureInfo.InvariantCulture);

        return $"{errorCode} {identifierText}";
    }
}
