using System.Text.Json;
using Analysis.Application;
using Analysis.Domain;

namespace Analysis.Infrastructure.Adapters;

internal static class Mapping
{
    public static T Guard<T>(Func<T> map)
    {
        try { return map(); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or
            FormatException or OverflowException or ArgumentException or IndexOutOfRangeException)
        { throw new ProviderReadException("schema-or-unit-mismatch"); }
    }

    public static decimal DecimalText(JsonElement value) => ExactDecimal.Parse(value.GetString() ?? throw new FormatException("Missing decimal text."));
    public static decimal DecimalNumber(JsonElement value) => value.ValueKind == JsonValueKind.Number
        ? ExactDecimal.Parse(value.GetRawText()) : throw new FormatException("Expected JSON number.");
    public static long IntegerText(JsonElement value) => long.Parse(value.GetString()!,
        System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);

    public static void Equal(string? actual, string expected)
    {
        if (actual != expected) throw new ProviderReadException("instrument-mismatch");
    }

    public static Observation Valid(Observation value, InstrumentRef instrument)
    {
        value.Validate(instrument);
        return value;
    }

    public static bool Within(DateTimeOffset time, ReadWindow window) => time >= window.StartUtc && time < window.EndUtc;
}
