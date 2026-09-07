using Analysis.Domain.Scoring;

namespace Analysis.Domain.SignalsOutcomes;

public sealed record ForwardItem(Asset Asset, int? Rank, string EligibilityReason,
    string ScoreSnapshotId, string FeatureSnapshotId, string ScoreHash, string FeatureHash,
    bool CorePriceReady, ScoreResult Score, Dictionary<string, int> FeatureStates);
public sealed record RelativeStrengthItem(string AssetId, int Rank, decimal Value,
    decimal AssetReturn24h, decimal BitcoinReturn24h, string FeatureSnapshotId, string BitcoinFeatureSnapshotId);
public sealed record RelativeStrengthOrdering(string State, string Reason, RelativeStrengthItem[] Items);
public sealed record ForwardOriginal(string Id, string RecordKind, string MethodologyId, string MethodologyHash,
    string MethodologySourceHash, string BatchId, string ModelId, string ManifestHash, string CalculatorSourceHash,
    string InputHash, DateTimeOffset AsOfUtc, DateTimeOffset KnowledgeCutoffUtc, DateTimeOffset CreatedAtUtc,
    DateTimeOffset IssuedAtUtc, DateTimeOffset ReferenceBoundaryUtc, int ReferenceDelayMilliseconds, string[] UniverseAssetIds,
    string[] EligibleAssetIds, InstrumentRef[] Instruments, ForwardItem[] Items, RelativeStrengthOrdering RelativeStrength);
public sealed record OutcomeTarget(string IssuanceId, string AssetId, string InstrumentId, int HorizonHours,
    DateTimeOffset ReferenceBoundaryUtc, DateTimeOffset MaturityUtc);
public sealed record OutcomeEvidence(ObservationFact[] Facts, ObservationKey[] Missing,
    ObservationKey[] Invalid, ConflictFact[] Conflicts, OutcomeRevision[] Revisions);
public sealed record OutcomeRevision(string Id, string QuarantineId, ObservationFact Original,
    Observation Candidate, string PayloadId, string MappingVersion, string PayloadSha256, DateTimeOffset DetectedAtUtc);
public sealed record OutcomeResult(OutcomeTarget Target, string State, string Reason, string QuoteUnit,
    string ReturnUnit, decimal? ReferencePrice, decimal? TerminalPrice, decimal? Return,
    OutcomeEvidence Evidence, string? OriginalCompleteAssessmentId = null);
public sealed record OutcomeCapture(DateTimeOffset KnowledgeCutoffUtc, ObservationFact[] Facts, ConflictFact[] Conflicts, OutcomeRevision[]? Revisions = null);
public sealed record StoredOutcome(string Id, DateTimeOffset AssessedAtUtc, DateTimeOffset KnowledgeCutoffUtc,
    string EvidenceHash, OutcomeResult Result);
public sealed record ForwardInspection(ForwardOriginal Original, StoredOutcome[] Outcomes,
    StoredOutcome[] History, OutcomeTarget[] UnassessedTargets);
public sealed record ForwardAggregate(string ModelId, string ManifestHash, string MethodologyId,
    string MethodologyHash, string EligibleSet, string QualityComposition, int HorizonHours,
    int TotalIssuances, int EligibleSamples, int PairedCompleteSamples, Dictionary<string, int> Exclusions,
    Dictionary<int, int> CompleteSamplesByRank, Dictionary<string, int> OutcomeStateCounts,
    Dictionary<string, string> AggregationIssues, Dictionary<int, decimal?> MeanReturnByRank, decimal? ModelTop1Mean, decimal? BitcoinMean,
    decimal? RelativeStrengthTop1Mean, decimal? ModelMinusBitcoinMean, decimal? ModelMinusRelativeStrengthMean);
