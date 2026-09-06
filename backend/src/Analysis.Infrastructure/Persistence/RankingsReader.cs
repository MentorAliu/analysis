using System.Data;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Infrastructure.Persistence;

// Deliberately independent of ScoringStore: HTTP reads neither facts nor calculators.
public sealed class RankingsReader(IDbContextFactory<ResearchDbContext> factory) : IRankingsReader
{
    public async Task<RankingsReadBatch> ReadAsync(RankingsRequest request, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Order(StringComparer.Ordinal);
        if (!applied.SequenceEqual(db.Database.GetMigrations().Order(StringComparer.Ordinal)))
            throw new RankingsReadException("schema-not-ready");

        var model = await db.Set<ScoringModelRow>().AsNoTracking().SingleOrDefaultAsync(m => m.Id == request.ModelId, cancellationToken)
            ?? throw new RankingsReadException("model-not-found");
        var query = db.Set<ScoringBatchRow>().AsNoTracking().Where(b => b.ModelId == request.ModelId);
        if (request.AsOfUtc.HasValue) query = query.Where(b => b.AsOfUtc == request.AsOfUtc.Value);
        // Never materialize InputJson (raw frozen observations) through this read boundary.
        var batch = await query.OrderByDescending(b => b.AsOfUtc).Select(b => new
        { b.Id, b.ModelId, b.AsOfUtc, b.KnowledgeCutoffUtc, b.CreatedAtUtc, b.RecordKind, b.UniverseJson, b.InputHash })
            .FirstOrDefaultAsync(cancellationToken) ?? throw new RankingsReadException("batch-not-found");
        var scores = await db.Set<ScoreSnapshotRow>().AsNoTracking().Where(s => s.BatchId == batch.Id).ToArrayAsync(cancellationToken);
        var categories = await db.Set<CategoryScoreRow>().AsNoTracking().Where(c => c.BatchId == batch.Id).ToArrayAsync(cancellationToken);
        var snapshots = await db.Set<FeatureSnapshotRow>().AsNoTracking().Where(f => f.BatchId == batch.Id).ToArrayAsync(cancellationToken);
        var states = await db.Set<FeatureValueRow>().AsNoTracking().Where(f => f.BatchId == batch.Id)
            .Select(f => new { f.SnapshotId, f.FeatureId, f.State }).ToArrayAsync(cancellationToken);
        var assets = await db.Assets.AsNoTracking().Where(a => a.Id == "bitcoin" || a.Id == "ethereum" || a.Id == "solana").ToArrayAsync(cancellationToken);
        try
        {
            var manifest = CanonicalJson.Read<ScoringManifest>(model.ManifestJson);
            Require(CanonicalJson.Hash(model.ManifestJson) == model.ManifestHash && manifest.ModelId == model.Id);
            Require(manifest.Universe.SequenceEqual(new[] { "bitcoin", "ethereum", "solana" }) &&
                batch.UniverseJson == CanonicalJson.Write(manifest.Universe) &&
                manifest.Numeric.ScorePlaces == 6 && manifest.Numeric.WeightDenominator > 0 &&
                manifest.Numeric.Version == "decimal18-v1" &&
                manifest.Features.Select(f => f.Id).SequenceEqual(Enumerable.Range(1, 21)));
            Require(batch.RecordKind == manifest.RecordKind && batch.RecordKind == "research-reconstruction" &&
                batch.AsOfUtc <= batch.KnowledgeCutoffUtc && batch.KnowledgeCutoffUtc <= batch.CreatedAtUtc &&
                batch.AsOfUtc.Ticks % TimeSpan.TicksPerHour == 0);
            foreach (var t in new[] { batch.AsOfUtc, batch.KnowledgeCutoffUtc, batch.CreatedAtUtc }) Utc.Require(t);
            Require(scores.Length == 3 && snapshots.Length == 3 && categories.Length == 12 && states.Length == 63 && assets.Length == 3);
            var items = manifest.Universe.Select(id =>
            {
                var row = scores.Single(s => s.AssetId == id);
                var snapshot = snapshots.Single(f => f.Id == row.SnapshotId);
                var score = CanonicalJson.Read<ScoreResult>(row.ScoreJson);
                Require(CanonicalJson.Hash(row.ScoreJson) == row.ScoreHash && row.Id == snapshot.Id &&
                    row.AsOfUtc == batch.AsOfUtc && row.ModelId == batch.ModelId &&
                    snapshot.AssetId == id && snapshot.AsOfUtc == batch.AsOfUtc && snapshot.ModelId == batch.ModelId &&
                    score.AssetId == id && score.AsOfUtc == row.AsOfUtc && score.ModelId == row.ModelId &&
                    score.State == row.State && score.Composite == row.Composite &&
                    score.BullishConfidence == row.BullishConfidence && score.BearishConfidence == row.BearishConfidence &&
                    score.DataQuality == row.DataQuality && score.ContextCoverage == row.ContextCoverage);
                Require(score.ProviderAgreement == "unassessed-single-source");
                Require(score.Categories.Select(c => c.Category).SequenceEqual(new[] { "price", "derivatives", "fundamentals", "regime" }));
                foreach (var c in score.Categories)
                {
                    var stored = categories.Single(s => s.ScoreId == row.Id && s.Category == c.Category);
                    Require(c == new CategoryScore(stored.Category, stored.State, stored.Score, stored.DataQuality, stored.ApplicableWeight, stored.AvailableWeight));
                }
                var featureStates = states.Where(f => f.SnapshotId == snapshot.Id).OrderBy(f => f.FeatureId).ToArray();
                Require(featureStates.Select(f => f.FeatureId).SequenceEqual(manifest.Features.Select(f => f.Id)) &&
                    featureStates.All(f => new[] { "available", "missing", "stale", "invalid", "conflicted", "inapplicable" }.Contains(f.State, StringComparer.Ordinal)));
                int Count(string state) => featureStates.Count(f => f.State == state);
                return new RankingReadItem(assets.Single(a => a.Id == id), row.Id, snapshot.Id, row.ScoreHash, snapshot.FeatureHash,
                    snapshot.CorePriceReady, new(Count("available"), Count("missing"), Count("stale"), Count("invalid"), Count("conflicted"), Count("inapplicable")), score);
            }).ToArray();
            var result = new RankingsReadBatch(batch.Id, batch.AsOfUtc, batch.KnowledgeCutoffUtc, batch.CreatedAtUtc,
                batch.RecordKind, batch.InputHash, manifest.Universe, manifest, model.ManifestHash, model.SourceHash, items);
            await tx.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or FormatException or System.Text.Json.JsonException or NullReferenceException)
        { throw new RankingsReadException("rankings-integrity-failure"); }
    }
    private static void Require(bool condition)
    { if (!condition) throw new RankingsReadException("rankings-integrity-failure"); }
}
