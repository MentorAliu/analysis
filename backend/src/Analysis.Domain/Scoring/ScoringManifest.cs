using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Analysis.Domain.Scoring;

public sealed record NumericPolicy(string Version, int Places, int ScorePlaces, string Midpoint, int WeightDenominator);
public sealed record HistoryPolicy(int CorePriceHours, int FundingGapHours, int TvlGapHours, decimal MinimumQuality);
public sealed record FeatureDefinition(int Id, string Key, string Operation, int Hours, string Unit,
    string Applicability, string Normalization, decimal? Threshold, int? ConfirmationFeatureId);
public sealed record EvidenceGroup(decimal Weight, int[] FeatureIds);
public sealed record CategoryDefinition(string Category, decimal Weight, EvidenceGroup[] Groups);
public sealed record ScoringManifest(string ModelId, string FeatureVersion, string ScorerVersion,
    string Status, string RecordKind, string[] Universe, Dictionary<string, string> BaseUnits, NumericPolicy Numeric, HistoryPolicy History,
    Dictionary<string, string> Policies, FeatureDefinition[] Features, Dictionary<string, CategoryDefinition[]> Profiles);

public sealed class ScoringModel
{
    private readonly string canonical;
    public string ManifestJson => canonical;
    // Return isolated values: callers cannot mutate the registered model through array properties.
    public ScoringManifest Manifest => CanonicalJson.Read<ScoringManifest>(canonical);
    public string Hash { get; }
    public string SourceHash { get; }
    public static ScoringModel Slice1 { get; } = Load();

    private ScoringModel(string json, string sourceHash)
    {
        canonical = CanonicalJson.Normalize(json);
        Hash = CanonicalJson.Hash(canonical); SourceHash = sourceHash;
        var m = Manifest;
        if (m.ModelId != "slice1-v1" || m.FeatureVersion != "slice1-features-v1" ||
            m.ScorerVersion != "slice1-scorer-v1" || m.Numeric != new NumericPolicy("decimal18-v1", 18, 6, "ToEven", 60000) ||
            !m.Features.Select(f => f.Id).SequenceEqual(Enumerable.Range(1, 21)) ||
            !m.Universe.SequenceEqual(new[] { "bitcoin", "ethereum", "solana" }))
            throw new InvalidOperationException("Unsupported scoring manifest.");
        foreach (var profile in m.Profiles.Values)
        {
            if (profile.Sum(c => c.Weight) != 1 || profile.Any(c => c.Groups.Sum(g => g.Weight) != 1))
                throw new InvalidOperationException("Invalid scoring weights.");
            var ids = new HashSet<int>();
            foreach (var category in profile)
                foreach (var group in category.Groups)
                    foreach (var id in group.FeatureIds)
                    {
                        var weight = m.Numeric.WeightDenominator * category.Weight * group.Weight / group.FeatureIds.Length;
                        if (weight <= 0 || weight != decimal.Truncate(weight) || !ids.Add(id) ||
                            m.Features.Single(f => f.Id == id).Normalization == "context")
                            throw new InvalidOperationException("Invalid evidence group.");
                    }
        }
    }

    private static ScoringModel Load()
    {
        var assembly = typeof(ScoringModel).Assembly;
        using var manifest = new StreamReader(assembly.GetManifestResourceStream(
            "Analysis.Domain.Scoring.Manifests.slice1-v1.json")!);
        var sources = assembly.GetManifestResourceNames().Where(n => n.EndsWith(".cs", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal).Select(name =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
                return new { name, text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal) };
            }).ToArray();
        return new(manifest.ReadToEnd(), CanonicalJson.Hash(CanonicalJson.Write(sources)));
    }
}

// Internal persistence/hash encoding, not an HTTP transport contract. Decimal strings are exact.
public static class CanonicalJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString |
            System.Text.Json.Serialization.JsonNumberHandling.WriteAsString
    };
    public static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options) ?? throw new FormatException("Missing document.");
    public static string Write<T>(T value) => Normalize(JsonSerializer.Serialize(value, Options));
    public static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public static string Normalize(string json)
    {
        using var doc = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Visit(doc.RootElement, writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void Visit(JsonElement element, Utf8JsonWriter writer)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var p in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(p.Name); Visit(p.Value, writer); }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        { writer.WriteStartArray(); foreach (var item in element.EnumerateArray()) Visit(item, writer); writer.WriteEndArray(); }
        else if (element.ValueKind == JsonValueKind.Number)
            writer.WriteRawValue(element.GetDecimal().ToString("0.############################", System.Globalization.CultureInfo.InvariantCulture));
        else element.WriteTo(writer);
    }
}
