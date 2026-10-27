using System.Text.Json;

namespace HouseholdFinances.Api.Conventions;

/// <summary>
/// The API's JSON conventions, applied to both MVC controllers and minimal API endpoints so the
/// two share one wire format.
/// </summary>
/// <remarks>
/// Conventions:
/// <list type="bullet">
/// <item>Property names are camelCase and are matched case-insensitively when read.</item>
/// <item>Enums are serialized as their numeric values (the System.Text.Json default). The
/// <c>Interval</c> values are mirrored by the sibling frontend, so a numeric representation needs
/// no translation. Do not add a <c>JsonStringEnumConverter</c>: that would be a breaking change
/// for the frontend.</item>
/// <item>Date-times cross the wire in UTC (see <see cref="UtcDateTimeJsonConverter"/>).</item>
/// </list>
/// </remarks>
public static class ApiJsonConventions
{
    /// <summary>Applies the API's JSON conventions to <paramref name="options"/>.</summary>
    /// <param name="options">The serializer options to configure.</param>
    public static void Apply(JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = JsonNamingPolicy.CamelCase;
        options.PropertyNameCaseInsensitive = true;

        if (!options.Converters.Any(converter => converter is UtcDateTimeJsonConverter))
        {
            options.Converters.Add(new UtcDateTimeJsonConverter());
        }
    }
}
