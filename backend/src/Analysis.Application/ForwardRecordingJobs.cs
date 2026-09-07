using Analysis.Domain;
using Analysis.Domain.Scoring;
using Analysis.Domain.SignalsOutcomes;

namespace Analysis.Application;

public sealed class ForwardPreconditionException(string code) : Exception(code);
public sealed record ForwardIssueResult(ForwardOriginal Original, bool Duplicate);
public interface IForwardRecordingStore
{
    Task<ForwardOriginal?> FindAsync(string modelId, DateTimeOffset asOfUtc, CancellationToken cancellationToken);
    Task<ScoringInput> CaptureAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken);
    Task<ForwardIssueResult> PublishAsync(ScoringBundle bundle, CancellationToken cancellationToken);
}
public sealed class ForwardRecordingJobs(IForwardRecordingStore store)
{
    public async Task<ForwardIssueResult> IssueAsync(string modelId, DateTimeOffset t, CancellationToken cancellationToken)
    {
        Utc.Require(t);
        if (modelId != ForwardMethodology.Manifest.ModelId || t != ForwardMethodology.Hour(t)) throw new ForwardPreconditionException("invalid-forward-request");
        var existing = await store.FindAsync(modelId, t, cancellationToken);
        if (existing is not null) return new(existing, true);
        var input = await store.CaptureAsync(t, cancellationToken);
        var bundle = ScoringJobs.Calculate(input, ScoringModel.Slice1, cancellationToken);
        return await store.PublishAsync(bundle, cancellationToken);
    }

    public static ForwardOriginal Original(StoredScoringBatch stored, Asset[] assets, DateTimeOffset created, DateTimeOffset issued)
    {
        var bundle = stored.Bundle; var input = bundle.Input;
        ForwardMethodology.RequireIssuanceClock(input.AsOfUtc, input.KnowledgeCutoffUtc, created, issued);
        var rank = 0;
        var items = RankingOrder.Sort(bundle.Assets, a => a.Score.Composite, a => a.Score.AssetId).Select(a =>
        {
            var s = a.Score; var f = a.Features;
            var ready = s.State is "complete" or "partial";
            if (ready != s.Composite.HasValue || ready && (!f.CorePriceReady || s.DataQuality < ScoringModel.Slice1.Manifest.History.MinimumQuality))
                throw new ForwardPreconditionException("invalid-score-readiness");
            var snapshot = CanonicalJson.Hash(CanonicalJson.Write(new { batchId = stored.Id, f.AssetId }));
            return new ForwardItem(assets.Single(asset => asset.Id == s.AssetId), ready ? ++rank : null,
                ready ? "m3-ready" : "m3-not-ready", snapshot, snapshot, CanonicalJson.Hash(CanonicalJson.Write(s)),
                CanonicalJson.Hash(CanonicalJson.Write(f)), f.CorePriceReady, s,
                new[] { "available", "conflicted", "inapplicable", "invalid", "missing", "stale" }
                    .ToDictionary(state => state, state => f.Values.Count(v => v.State == state)));
        }).ToArray();
        var manifest = ForwardMethodology.Manifest;
        var id = CanonicalJson.Hash(CanonicalJson.Write(new { stored.ModelId, input.AsOfUtc }));
        return new(id, "forward-issued-ranking", manifest.Id, ForwardMethodology.Hash, ForwardMethodology.SourceHash,
            stored.Id, stored.ModelId, stored.ManifestHash, stored.SourceHash, CanonicalJson.Hash(CanonicalJson.Write(input)),
            input.AsOfUtc, input.KnowledgeCutoffUtc, created, issued, ForwardMethodology.Hour(issued).AddHours(1),
            checked((int)(ForwardMethodology.Hour(issued).AddHours(1) - issued).TotalMilliseconds),
            manifest.Universe, items.Where(i => i.Rank.HasValue).Select(i => i.Asset.Id).ToArray(), input.Instruments,
            items, BenchmarkCalculator.Order(bundle, items));
    }
}
