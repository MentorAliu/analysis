using Analysis.Domain.Scoring;

namespace Analysis.Application;

public sealed record ScoreRequest(DateTimeOffset AsOfUtc, DateTimeOffset KnowledgeCutoffUtc, string ModelId);
public sealed record StoredScoringBatch(string Id, string ModelId, string ManifestHash, string SourceHash,
    ScoringBundle Bundle, bool Duplicate);
public sealed record ReplayReport(int Batches, int Scores, DateTimeOffset[] MissingPeriods);
public sealed class ScoringPreconditionException(string code) : Exception(code);

public interface IScoringInputReader
{
    Task<ScoringInput> CaptureAsync(ScoreRequest request, ScoringModel model, CancellationToken cancellationToken);
}
public interface IScoringStore
{
    Task<StoredScoringBatch?> FindAsync(DateTimeOffset asOfUtc, string modelId, CancellationToken cancellationToken);
    Task<StoredScoringBatch> PublishAsync(ScoringBundle bundle, ScoringModel model, DateTimeOffset createdAtUtc, CancellationToken cancellationToken);
    Task<StoredScoringBatch[]> ReadRangeAsync(string modelId, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken);
}

public sealed class ScoringJobs(IScoringInputReader reader, IScoringStore store, TimeProvider clock)
{
    private static ScoringModel Model(string modelId)
    {
        if (modelId != ScoringModel.Slice1.Manifest.ModelId) throw new ScoringPreconditionException("unsupported-model");
        return ScoringModel.Slice1;
    }
    public async Task<StoredScoringBatch> RunAsync(ScoreRequest request, CancellationToken cancellationToken)
    {
        var model = Model(request.ModelId);
        Analysis.Domain.Utc.Require(request.AsOfUtc); Analysis.Domain.Utc.Require(request.KnowledgeCutoffUtc);
        if (request.AsOfUtc.ToUnixTimeMilliseconds() % 3_600_000 != 0 || request.KnowledgeCutoffUtc < request.AsOfUtc || request.KnowledgeCutoffUtc > clock.GetUtcNow())
            throw new ScoringPreconditionException("invalid-scoring-clock");
        var existing = await store.FindAsync(request.AsOfUtc, request.ModelId, cancellationToken);
        if (existing is not null)
        {
            if (existing.Bundle.Input.KnowledgeCutoffUtc != request.KnowledgeCutoffUtc)
                throw new ScoringPreconditionException("input-cutoff-conflict");
            Verify(existing, model);
            return existing with { Duplicate = true };
        }
        var input = await reader.CaptureAsync(request, model, cancellationToken);
        var bundle = Calculate(input, model, cancellationToken);
        var createdAt = DateTimeOffset.FromUnixTimeMilliseconds(clock.GetUtcNow().ToUnixTimeMilliseconds());
        var result = await store.PublishAsync(bundle, model, createdAt, cancellationToken);
        Verify(result, model);
        return result;
    }
    public async Task<ReplayReport> ReplayAsync(string modelId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var model = Model(modelId);
        Analysis.Domain.Utc.Require(start); Analysis.Domain.Utc.Require(end);
        if (end <= start || end - start > TimeSpan.FromDays(7) ||
            start.ToUnixTimeMilliseconds() % 3_600_000 != 0 || end.ToUnixTimeMilliseconds() % 3_600_000 != 0)
            throw new ScoringPreconditionException("invalid-replay-range");
        var batches = await store.ReadRangeAsync(modelId, start, end, cancellationToken);
        foreach (var batch in batches) { cancellationToken.ThrowIfCancellationRequested(); Verify(batch, model); }
        var periods = batches.Select(b => b.Bundle.Input.AsOfUtc).ToHashSet();
        var missing = new List<DateTimeOffset>();
        for (var t = start; t < end; t = t.AddHours(1)) if (!periods.Contains(t)) missing.Add(t);
        return new(batches.Length, batches.Sum(b => b.Bundle.Assets.Length), missing.ToArray());
    }
    public static ScoringBundle Calculate(ScoringInput input, ScoringModel model, CancellationToken cancellationToken = default)
    {
        var features = new FeatureCalculator(model); var scores = new ScoreCalculator(model);
        return new(input, model.Manifest.Universe.Select(asset =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var values = features.Calculate(asset, input);
            return new AssetCalculation(values, scores.Calculate(values));
        }).ToArray());
    }
    public static void Verify(StoredScoringBatch stored, ScoringModel model)
    {
        if (stored.ModelId != model.Manifest.ModelId || stored.ManifestHash != model.Hash || stored.SourceHash != model.SourceHash)
            throw new InvalidOperationException("Model identity mismatch.");
        var replay = Calculate(stored.Bundle.Input, model);
        if (CanonicalJson.Write(replay) != CanonicalJson.Write(stored.Bundle)) throw new InvalidOperationException("Scoring replay mismatch.");
    }
}
