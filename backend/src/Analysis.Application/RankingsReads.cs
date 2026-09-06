using Analysis.Domain;
using Analysis.Domain.Scoring;

namespace Analysis.Application;

public sealed record RankingsRequest(string ModelId, DateTimeOffset? AsOfUtc);
public sealed record RankingFeatureCounts(int Available, int Missing, int Stale, int Invalid, int Conflicted, int Inapplicable);
public sealed record RankingReadItem(Asset Asset, string ScoreSnapshotId, string FeatureSnapshotId,
    string ScoreHash, string FeatureHash, bool CorePriceReady, RankingFeatureCounts FeatureStateCounts, ScoreResult Score);
public sealed record RankingsReadBatch(string Id, DateTimeOffset AsOfUtc, DateTimeOffset KnowledgeCutoffUtc,
    DateTimeOffset CreatedAtUtc, string RecordKind, string InputHash, string[] UniverseAssetIds,
    ScoringManifest Manifest, string ManifestHash, string CalculatorSourceHash, RankingReadItem[] Items);
public sealed class RankingsReadException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
public interface IRankingsReader
{
    Task<RankingsReadBatch> ReadAsync(RankingsRequest request, CancellationToken cancellationToken);
}
