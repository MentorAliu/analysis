using System.Data;
using System.Security.Cryptography;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Infrastructure.Persistence;

public sealed class ScoringStore(IDbContextFactory<ResearchDbContext> factory) : IScoringInputReader, IScoringStore
{
    public async Task<ScoringInput> CaptureAsync(ScoreRequest request, ScoringModel model, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        await PreconditionsAsync(db, cancellationToken);
        var m = model.Manifest;
        var lookback = m.Features.Max(f => f.Operation switch
        {
            "tvl" or "tvl-change" => f.Hours + m.History.TvlGapHours,
            "funding-change" => f.Hours * 2 + m.History.FundingGapHours,
            "funding-last" or "funding-sum" => f.Hours + m.History.FundingGapHours,
            "quote-volume-change" => f.Hours * 2,
            _ => f.Hours + 1
        });
        var start = request.AsOfUtc.AddHours(-Math.Max(lookback, m.History.CorePriceHours + 1));
        var observations = await (from o in db.Observations.AsNoTracking()
            join p in db.Payloads.AsNoTracking() on o.PayloadId equals p.Id
            where o.EventTimeUtc >= start && o.EventTimeUtc <= request.AsOfUtc && o.IngestedAtUtc <= request.KnowledgeCutoffUtc
            select new { Observation = o, p.MappingVersion, p.Sha256 }).ToArrayAsync(cancellationToken);
        var facts = observations.Where(x => x.Observation.Kind != ObservationKind.Candle ||
                x.Observation.EventTimeUtc.AddSeconds(x.Observation.PeriodSeconds) <= request.AsOfUtc)
            .Select(x => new ObservationFact(x.Observation.ToObservation(), x.Observation.PayloadId,
                x.MappingVersion, x.Sha256, x.Observation.IngestedAtUtc))
            .OrderBy(x => x.Observation.InstrumentId, StringComparer.Ordinal).ThenBy(x => x.Observation.Kind)
            .ThenBy(x => x.Observation.EventTimeUtc).ThenBy(x => x.Observation.PeriodSeconds).ToArray();
        var conflicts = await db.Quarantine.AsNoTracking().Where(q => q.Code == "conflicting-observation" &&
            q.WindowEndUtc > start && q.WindowStartUtc <= request.AsOfUtc && q.IngestedAtUtc <= request.KnowledgeCutoffUtc)
            .ToArrayAsync(cancellationToken);
        var instruments = await db.Instruments.AsNoTracking().ToArrayAsync(cancellationToken);
        var input = new ScoringInput(request.AsOfUtc, request.KnowledgeCutoffUtc,
            instruments.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray(), facts,
            conflicts.OrderBy(q => q.Id, StringComparer.Ordinal).Select(q => new ConflictFact(q.Id, q.InstrumentId,
                q.WindowStartUtc, q.WindowEndUtc, q.IngestedAtUtc, q.Code)).ToArray());
        await transaction.CommitAsync(cancellationToken);
        return input;
    }

    public static async Task PreconditionsAsync(ResearchDbContext db, CancellationToken cancellationToken)
    {
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any()) throw new ScoringPreconditionException("pending-migrations");
        var assets = await db.Assets.AsNoTracking().ToArrayAsync(cancellationToken);
        var instruments = await db.Instruments.AsNoTracking().ToArrayAsync(cancellationToken);
        if (!assets.OrderBy(a => a.Id, StringComparer.Ordinal).SequenceEqual(CatalogSeed.Assets.OrderBy(a => a.Id, StringComparer.Ordinal)) ||
            !instruments.OrderBy(i => i.Id, StringComparer.Ordinal).SequenceEqual(CatalogSeed.Instruments.OrderBy(i => i.Id, StringComparer.Ordinal)))
            throw new ScoringPreconditionException("catalog-mismatch");
    }

    public async Task<StoredScoringBatch?> FindAsync(DateTimeOffset asOfUtc, string modelId, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var batch = await db.Set<ScoringBatchRow>().AsNoTracking().SingleOrDefaultAsync(b => b.AsOfUtc == asOfUtc && b.ModelId == modelId, cancellationToken);
        return batch is null ? null : await MaterializeAsync(db, batch, true, cancellationToken);
    }

    public async Task<StoredScoringBatch> PublishAsync(ScoringBundle bundle, ScoringModel model,
        DateTimeOffset createdAtUtc, CancellationToken cancellationToken)
    {
        var m = model.Manifest; Utc.Require(createdAtUtc);
        if (createdAtUtc < bundle.Input.KnowledgeCutoffUtc || CanonicalJson.Write(ScoringJobs.Calculate(bundle.Input, model)) != CanonicalJson.Write(bundle))
            throw new ArgumentException("Invalid scoring bundle.");
        var inputJson = CanonicalJson.Write(bundle.Input); var inputHash = CanonicalJson.Hash(inputJson);
        var id = CanonicalJson.Hash(CanonicalJson.Write(new { m.ModelId, bundle.Input.AsOfUtc, bundle.Input.KnowledgeCutoffUtc }));
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"m3-model:" + m.ModelId}, 0))", cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({LockKey(m.ModelId, bundle.Input.AsOfUtc)}, 0))", cancellationToken);
        var known = await db.Set<ScoringModelRow>().SingleOrDefaultAsync(v => v.Id == m.ModelId, cancellationToken);
        if (known is not null && (known.ManifestJson != model.ManifestJson || known.ManifestHash != model.Hash || known.SourceHash != model.SourceHash))
            throw new ScoringPreconditionException("model-version-conflict");
        var existing = await db.Set<ScoringBatchRow>().AsNoTracking().SingleOrDefaultAsync(b => b.ModelId == m.ModelId && b.AsOfUtc == bundle.Input.AsOfUtc, cancellationToken);
        if (existing is not null)
        {
            if (existing.KnowledgeCutoffUtc != bundle.Input.KnowledgeCutoffUtc) throw new ScoringPreconditionException("input-cutoff-conflict");
            var winner = await MaterializeAsync(db, existing, true, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return winner;
        }
        await VerifySourceFactsAsync(db, bundle.Input, cancellationToken);
        if (known is null) db.Add(new ScoringModelRow { Id = m.ModelId, ManifestJson = model.ManifestJson,
            ManifestHash = model.Hash, SourceHash = model.SourceHash, CreatedAtUtc = createdAtUtc });
        db.Add(new ScoringBatchRow { Id = id, ModelId = m.ModelId, AsOfUtc = bundle.Input.AsOfUtc,
            KnowledgeCutoffUtc = bundle.Input.KnowledgeCutoffUtc, CreatedAtUtc = createdAtUtc, RecordKind = m.RecordKind,
            UniverseJson = CanonicalJson.Write(m.Universe), InputJson = inputJson, InputHash = inputHash });
        // Make the parent visible inside this transaction before guarded child insertion.
        await db.SaveChangesAsync(cancellationToken);
        foreach (var fact in bundle.Input.Observations)
        {
            var o = fact.Observation;
            db.Add(new InputObservationRow { BatchId = id, InstrumentId = o.InstrumentId, Kind = o.Kind,
                EventTimeUtc = o.EventTimeUtc, PeriodSeconds = o.PeriodSeconds, FactJson = CanonicalJson.Write(fact) });
        }
        foreach (var conflict in bundle.Input.Conflicts)
            db.Add(new InputConflictRow { BatchId = id, ConflictId = conflict.Id, FactJson = CanonicalJson.Write(conflict) });
        var inputKeys = bundle.Input.Observations.Select(f => ObservationKey.Of(f.Observation)).ToHashSet();
        var conflictIds = bundle.Input.Conflicts.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var asset in bundle.Assets)
        {
            var f = asset.Features; var s = asset.Score;
            if (f.Values.Any(v => v.Inputs.Any(k => !inputKeys.Contains(k)) || v.ConflictIds.Any(c => !conflictIds.Contains(c))))
                throw new ArgumentException("Feature lineage outside snapshot.");
            var snapshotId = CanonicalJson.Hash(CanonicalJson.Write(new { batchId = id, f.AssetId }));
            db.Add(new FeatureSnapshotRow { Id = snapshotId, BatchId = id, AssetId = f.AssetId, ModelId = f.ModelId,
                AsOfUtc = f.AsOfUtc, CorePriceReady = f.CorePriceReady, FeatureHash = CanonicalJson.Hash(CanonicalJson.Write(f)) });
            foreach (var value in f.Values) db.Add(new FeatureValueRow { SnapshotId = snapshotId, BatchId = id,
                FeatureId = value.Id, Key = value.Key, CalculationVersion = value.CalculationVersion, Unit = value.Unit,
                State = value.State, Value = value.Value, DetailJson = CanonicalJson.Write(value) });
            var scoreJson = CanonicalJson.Write(s);
            db.Add(new ScoreSnapshotRow { Id = snapshotId, SnapshotId = snapshotId, BatchId = id, AssetId = s.AssetId,
                ModelId = s.ModelId, AsOfUtc = s.AsOfUtc, State = s.State, Composite = s.Composite,
                BullishConfidence = s.BullishConfidence, BearishConfidence = s.BearishConfidence, DataQuality = s.DataQuality,
                ContextCoverage = s.ContextCoverage, ScoreJson = scoreJson, ScoreHash = CanonicalJson.Hash(scoreJson) });
            foreach (var category in s.Categories) db.Add(new CategoryScoreRow { ScoreId = snapshotId, BatchId = id,
                Category = category.Category, State = category.State, Score = category.Score, DataQuality = category.DataQuality,
                ApplicableWeight = category.ApplicableWeight, AvailableWeight = category.AvailableWeight });
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(id, m.ModelId, model.Hash, model.SourceHash, bundle, false);
    }

    public async Task<StoredScoringBatch[]> ReadRangeAsync(string modelId, DateTimeOffset startUtc,
        DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var batches = await db.Set<ScoringBatchRow>().AsNoTracking().Where(b => b.ModelId == modelId &&
            b.AsOfUtc >= startUtc && b.AsOfUtc < endUtc).OrderBy(b => b.AsOfUtc).ToArrayAsync(cancellationToken);
        var results = new List<StoredScoringBatch>();
        foreach (var b in batches) results.Add(await MaterializeAsync(db, b, true, cancellationToken));
        return results.ToArray();
    }

    private static async Task<StoredScoringBatch> MaterializeAsync(ResearchDbContext db, ScoringBatchRow batch,
        bool duplicate, CancellationToken cancellationToken)
    {
        var model = await db.Set<ScoringModelRow>().AsNoTracking().SingleAsync(m => m.Id == batch.ModelId, cancellationToken);
        Require(CanonicalJson.Hash(model.ManifestJson) == model.ManifestHash && CanonicalJson.Hash(batch.InputJson) == batch.InputHash);
        var input = CanonicalJson.Read<ScoringInput>(batch.InputJson);
        var manifest = CanonicalJson.Read<ScoringManifest>(model.ManifestJson);
        Require(input.AsOfUtc == batch.AsOfUtc && input.KnowledgeCutoffUtc == batch.KnowledgeCutoffUtc &&
            batch.RecordKind == manifest.RecordKind && batch.UniverseJson == CanonicalJson.Write(manifest.Universe));
        var inputRows = await db.Set<InputObservationRow>().AsNoTracking().Where(r => r.BatchId == batch.Id).ToArrayAsync(cancellationToken);
        var expected = input.Observations.ToDictionary(f => ObservationKey.Of(f.Observation));
        Require(inputRows.Length == expected.Count);
        foreach (var row in inputRows)
            Require(expected.TryGetValue(new(row.InstrumentId, row.Kind, row.EventTimeUtc, row.PeriodSeconds), out var fact) && row.FactJson == CanonicalJson.Write(fact));
        var conflictRows = await db.Set<InputConflictRow>().AsNoTracking().Where(r => r.BatchId == batch.Id).ToArrayAsync(cancellationToken);
        Require(conflictRows.Length == input.Conflicts.Length);
        foreach (var c in conflictRows) Require(input.Conflicts.Any(f => f.Id == c.ConflictId && c.FactJson == CanonicalJson.Write(f)));
        await VerifySourceFactsAsync(db, input, cancellationToken);
        var snapshots = await db.Set<FeatureSnapshotRow>().AsNoTracking().Where(r => r.BatchId == batch.Id).ToArrayAsync(cancellationToken);
        var values = await db.Set<FeatureValueRow>().AsNoTracking().Where(r => r.BatchId == batch.Id).ToArrayAsync(cancellationToken);
        var scores = await db.Set<ScoreSnapshotRow>().AsNoTracking().Where(r => r.BatchId == batch.Id).ToArrayAsync(cancellationToken);
        var categories = await db.Set<CategoryScoreRow>().AsNoTracking().Where(r => r.BatchId == batch.Id).ToArrayAsync(cancellationToken);
        Require(snapshots.Length == manifest.Universe.Length && scores.Length == manifest.Universe.Length &&
            values.Length == manifest.Universe.Length * manifest.Features.Length && categories.Length == manifest.Universe.Length * 4);
        var calculations = new List<AssetCalculation>();
        foreach (var asset in manifest.Universe)
        {
            var snapshot = snapshots.Single(s => s.AssetId == asset);
            var featureValues = values.Where(v => v.SnapshotId == snapshot.Id).OrderBy(v => v.FeatureId).Select(row =>
            {
                var value = CanonicalJson.Read<FeatureValue>(row.DetailJson);
                Require(value.Id == row.FeatureId && value.Key == row.Key && value.CalculationVersion == row.CalculationVersion &&
                    value.Unit == row.Unit && value.State == row.State && value.Value == row.Value &&
                    value.Inputs.All(expected.ContainsKey) && value.ConflictIds.All(id => input.Conflicts.Any(c => c.Id == id)));
                return value;
            }).ToArray();
            var set = new FeatureSet(asset, snapshot.AsOfUtc, snapshot.ModelId, snapshot.CorePriceReady, featureValues);
            Require(CanonicalJson.Hash(CanonicalJson.Write(set)) == snapshot.FeatureHash);
            var row = scores.Single(s => s.SnapshotId == snapshot.Id); var result = CanonicalJson.Read<ScoreResult>(row.ScoreJson);
            Require(CanonicalJson.Hash(row.ScoreJson) == row.ScoreHash && result.AssetId == row.AssetId && result.AsOfUtc == row.AsOfUtc &&
                result.ModelId == row.ModelId && result.State == row.State && result.Composite == row.Composite &&
                result.BullishConfidence == row.BullishConfidence && result.BearishConfidence == row.BearishConfidence &&
                result.DataQuality == row.DataQuality && result.ContextCoverage == row.ContextCoverage);
            foreach (var category in result.Categories)
            {
                var c = categories.Single(c => c.ScoreId == row.Id && c.Category == category.Category);
                Require(category == new CategoryScore(c.Category, c.State, c.Score, c.DataQuality, c.ApplicableWeight, c.AvailableWeight));
            }
            calculations.Add(new(set, result));
        }
        return new(batch.Id, batch.ModelId, model.ManifestHash, model.SourceHash, new(input, calculations.ToArray()), duplicate);
    }

    private static async Task VerifySourceFactsAsync(ResearchDbContext db, ScoringInput input, CancellationToken cancellationToken)
    {
        var payloadIds = input.Observations.Select(f => f.PayloadId).Distinct().ToArray();
        var payloads = await db.Payloads.AsNoTracking().Where(p => payloadIds.Contains(p.Id)).ToArrayAsync(cancellationToken);
        var observations = await db.Observations.AsNoTracking().Where(o => payloadIds.Contains(o.PayloadId)).ToArrayAsync(cancellationToken);
        var map = observations.ToDictionary(o => new ObservationKey(o.InstrumentId, o.Kind, o.EventTimeUtc, o.PeriodSeconds));
        foreach (var p in payloads) Require(Convert.ToHexStringLower(SHA256.HashData(p.Bytes)) == p.Sha256);
        foreach (var f in input.Observations)
        {
            Require(map.TryGetValue(ObservationKey.Of(f.Observation), out var o));
            var p = payloads.Single(p => p.Id == f.PayloadId);
            Require(o!.ToObservation() == f.Observation && o.IngestedAtUtc == f.IngestedAtUtc && o.PayloadId == f.PayloadId &&
                p.InstrumentId == f.Observation.InstrumentId && p.MappingVersion == f.MappingVersion && p.Sha256 == f.PayloadSha256 &&
                f.IngestedAtUtc <= input.KnowledgeCutoffUtc && f.Observation.EventTimeUtc <= input.AsOfUtc);
        }
        var conflictIds = input.Conflicts.Select(c => c.Id).ToArray();
        var conflicts = await db.Quarantine.AsNoTracking().Where(c => conflictIds.Contains(c.Id)).ToArrayAsync(cancellationToken);
        Require(conflicts.Length == input.Conflicts.Length);
        foreach (var c in input.Conflicts)
        {
            var stored = conflicts.Single(s => s.Id == c.Id);
            Require(c == new ConflictFact(stored.Id, stored.InstrumentId, stored.WindowStartUtc, stored.WindowEndUtc, stored.IngestedAtUtc, stored.Code));
        }
    }
    public static string LockKey(string modelId, DateTimeOffset asOf) => $"m3-batch:{modelId}:{asOf:O}";
    private static void Require(bool condition) { if (!condition) throw new InvalidOperationException("Stored scoring integrity mismatch."); }
}
