using System.Text.Json;
using System.Text.Json.Serialization;

namespace HouseholdFinances.Api.Conventions;

/// <summary>
/// Applies the recorded UTC date-time convention to the wire format: every
/// <see cref="DateTime"/> is written as ISO 8601 round-trip ("O") in UTC, and every incoming
/// value is returned as UTC. A value with an unspecified kind is treated as already being UTC,
/// which is the convention all callers must follow.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    /// <inheritdoc />
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => ToUtc(reader.GetDateTime());

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
        => writer.WriteStringValue(ToUtc(value));

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
