using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Analysis.Api.Rankings;

[AttributeUsage(AttributeTargets.Property)]
public sealed class WireAttribute(string pattern, string? format = null) : Attribute
{
    public string Pattern { get; } = pattern;
    public string? Format { get; } = format;
}
public static class Wire
{
    // Unlike $ alone, this portable .NET/ECMAScript ending rejects a final newline.
    public const string End = @"$(?![\s\S])";
    public const string Unsigned = @"^(?:100\.000000|(?:0|[1-9][0-9]?)\.[0-9]{6})" + End;
    public const string Signed = @"^(?!-0\.000000$)-?(?:100\.000000|(?:0|[1-9][0-9]?)\.[0-9]{6})" + End;
    public const string Hash = "^[0-9a-f]{64}" + End;
    public const string Timestamp = @"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}Z" + End;
    public const string Hour = "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:00:00Z" + End;
    public const string ModelId = "^[a-z0-9][a-z0-9._-]{0,63}" + End;
}
[JsonConverter(typeof(JsonStringEnumConverter<RankingSelection>))]
public enum RankingSelection { latest, exact }
[JsonConverter(typeof(JsonStringEnumConverter<RankingState>))]
public enum RankingState { complete, partial, [JsonStringEnumMemberName("not-ready")] NotReady }
[JsonConverter(typeof(JsonStringEnumConverter<CategoryState>))]
public enum CategoryState { complete, partial, missing, inapplicable }
[JsonConverter(typeof(JsonStringEnumConverter<CategoryName>))]
public enum CategoryName { price, derivatives, fundamentals, regime }

public sealed record RankingsResponse(RankingSelection Selection,
    [property: Wire(Wire.Hour, "date-time")] string? RequestedAsOfUtc,
    [property: Wire(Wire.Timestamp, "date-time")] string RetrievedAtUtc,
    [property: Range(0, 315537897599L)] long AsOfAgeSeconds,
    [property: Wire("^score-points" + Wire.End)] string ScoreUnit, RankingBatch Batch,
    [property: MinLength(3), MaxLength(3)] RankingItem[] Items);
public sealed record RankingBatch([property: Wire(Wire.Hash)] string Id,
    [property: Wire(Wire.Timestamp, "date-time")] string AsOfUtc,
    [property: Wire(Wire.Timestamp, "date-time")] string KnowledgeCutoffUtc,
    [property: Wire(Wire.Timestamp, "date-time")] string CreatedAtUtc,
    [property: Wire("^research-reconstruction" + Wire.End)] string RecordKind,
    [property: Wire(Wire.Hash)] string InputHash,
    [property: MinLength(3), MaxLength(3)] string[] UniverseAssetIds, RankingModel Model);
public sealed record RankingModel([property: Wire(Wire.ModelId)] string Id,
    [property: Wire(Wire.Hash)] string ManifestHash,
    [property: Wire(Wire.Hash)] string CalculatorSourceHash,
    string FeatureVersion, string ScorerVersion, string NumericVersion, string Status,
    [property: Range(1, int.MaxValue)] int WeightDenominator);
public sealed record RankingItem(string AssetId, string Symbol, string Name,
    [property: Range(1, 3)] int? Rank,
    [property: Wire(Wire.Hash)] string ScoreSnapshotId,
    [property: Wire(Wire.Hash)] string FeatureSnapshotId,
    [property: Wire(Wire.Hash)] string ScoreHash,
    [property: Wire(Wire.Hash)] string FeatureHash,
    RankingState State, [property: Wire(Wire.Signed)] string? CompositeScore,
    [property: Wire(Wire.Unsigned)] string? BullishConfidenceScore,
    [property: Wire(Wire.Unsigned)] string? BearishConfidenceScore,
    RankingQuality Quality, [property: MinLength(4), MaxLength(4)] RankingCategory[] Categories);
public sealed record RankingQuality([property: Wire(Wire.Unsigned)] string DataQualityPercent,
    [property: Wire(Wire.Unsigned)] string ContextCoveragePercent,
    [property: Wire("^unassessed-single-source" + Wire.End)] string ProviderAgreement,
    bool CorePriceReady, FeatureStateCounts FeatureStateCounts);
public sealed record FeatureStateCounts([property: Range(0, 21)] int Available,
    [property: Range(0, 21)] int Missing, [property: Range(0, 21)] int Stale,
    [property: Range(0, 21)] int Invalid, [property: Range(0, 21)] int Conflicted,
    [property: Range(0, 21)] int Inapplicable);
public sealed record RankingCategory(CategoryName Category, CategoryState State,
    [property: Wire(Wire.Signed)] string? Score,
    [property: Wire(Wire.Unsigned)] string DataQualityPercent,
    [property: Range(0, int.MaxValue)] int ApplicableWeightNumerator,
    [property: Range(0, int.MaxValue)] int AvailableWeightNumerator);

// The same declared problem shape describes explicit endpoint errors and the
// existing sanitized unexpected-error handler. M1 does not supply code/instance.
public sealed record RankingsProblem(string Type, string Title, int Status, string CorrelationId,
    string TraceId, string? Instance = null, string? Code = null, Dictionary<string, string[]>? Errors = null);
