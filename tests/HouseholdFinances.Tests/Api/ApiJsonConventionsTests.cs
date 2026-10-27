using System.Text.Json;
using HouseholdFinances.Api.Conventions;
using HouseholdFinances.Domain.Entities;

namespace HouseholdFinances.Tests.Api;

/// <summary>
/// Verifies the JSON conventions applied to controllers and minimal API endpoints: camelCase
/// property names, numeric enums, and UTC date-times.
/// </summary>
public class ApiJsonConventionsTests
{
    [Fact]
    public void Apply_UsesCamelCasePropertyNames()
    {
        var json = JsonSerializer.Serialize(new Sample { SomeValue = 1 }, CreateOptions());

        Assert.StartsWith("{\"someValue\":1", json);
        Assert.DoesNotContain("\"SomeValue\"", json);
    }

    [Fact]
    public void Apply_SerializesEnumsAsNumericValues()
    {
        var json = JsonSerializer.Serialize(new Sample { Interval = Interval.Quarterly }, CreateOptions());

        // Quarterly = 4; the sibling frontend mirrors these numeric values, so no string converter
        // may be introduced without a coordinated frontend change.
        Assert.Contains("\"interval\":4", json);
    }

    [Fact]
    public void Apply_WritesUnspecifiedDateTimesAsUtc()
    {
        var unspecified = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Unspecified);

        var json = JsonSerializer.Serialize(new Sample { When = unspecified }, CreateOptions());

        using var document = JsonDocument.Parse(json);
        var when = document.RootElement.GetProperty("when").GetString();

        // An unspecified kind has no zone designator on its own; the trailing Z proves the
        // converter treated the value as UTC.
        Assert.StartsWith("2026-01-02T03:04:05", when);
        Assert.EndsWith("Z", when);
    }

    [Fact]
    public void Apply_ReadsDateTimesBackAsUtc()
    {
        var options = CreateOptions();

        var roundTripped = JsonSerializer.Deserialize<Sample>(
            "{\"when\":\"2026-01-02T03:04:05.0000000Z\"}",
            options);

        Assert.NotNull(roundTripped);
        Assert.Equal(DateTimeKind.Utc, roundTripped!.When.Kind);
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5), roundTripped.When);
    }

    [Fact]
    public void Apply_MatchesPropertyNamesCaseInsensitively()
    {
        var value = JsonSerializer.Deserialize<Sample>("{\"SOMEVALUE\":9}", CreateOptions());

        Assert.Equal(9, value!.SomeValue);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        ApiJsonConventions.Apply(options);

        return options;
    }

    private sealed class Sample
    {
        public int SomeValue { get; set; }

        public Interval Interval { get; set; }

        public DateTime When { get; set; }
    }
}
