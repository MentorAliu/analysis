namespace Analysis.Domain.Scoring;

public sealed record ObservationKey(string InstrumentId, ObservationKind Kind, DateTimeOffset EventTimeUtc, int PeriodSeconds)
{
    public static ObservationKey Of(Observation o) => new(o.InstrumentId, o.Kind, o.EventTimeUtc, o.PeriodSeconds);
}
public sealed record ObservationFact(Observation Observation, string PayloadId, string MappingVersion,
    string PayloadSha256, DateTimeOffset IngestedAtUtc);
public sealed record ConflictFact(string Id, string InstrumentId, DateTimeOffset StartUtc,
    DateTimeOffset EndUtc, DateTimeOffset IngestedAtUtc, string Code);
public sealed record ScoringInput(DateTimeOffset AsOfUtc, DateTimeOffset KnowledgeCutoffUtc,
    InstrumentRef[] Instruments, ObservationFact[] Observations, ConflictFact[] Conflicts);
public sealed record InputWindow(string InstrumentId, ObservationKind Kind, DateTimeOffset StartUtc,
    DateTimeOffset EndUtc, bool EndInclusive, string Rule);
public sealed record FeatureValue(int Id, string Key, string CalculationVersion, string Unit,
    string State, string Reason, decimal? Value, ObservationKey[] Inputs, string[] ConflictIds,
    InputWindow[] Windows, DateTimeOffset? FirstEventUtc, DateTimeOffset? LastEventUtc,
    long? ElapsedMilliseconds);
public sealed record FeatureSet(string AssetId, DateTimeOffset AsOfUtc, string ModelId,
    bool CorePriceReady, FeatureValue[] Values);
public sealed record EvidenceValue(int FeatureId, string Category, int WeightNumerator,
    int WeightDenominator, decimal? Normalized, string State);
public sealed record CategoryScore(string Category, string State, decimal? Score,
    decimal DataQuality, int ApplicableWeight, int AvailableWeight);
public sealed record ScoreResult(string AssetId, DateTimeOffset AsOfUtc, string ModelId,
    string State, decimal? Composite, decimal? BullishConfidence, decimal? BearishConfidence,
    decimal DataQuality, decimal ContextCoverage, string ProviderAgreement,
    CategoryScore[] Categories, EvidenceValue[] Evidence);
public sealed record AssetCalculation(FeatureSet Features, ScoreResult Score);
public sealed record ScoringBundle(ScoringInput Input, AssetCalculation[] Assets);
