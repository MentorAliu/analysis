using System.Data;
using System.Text.Json;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Analysis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Analysis.ScoringChecks;

internal static class DatabaseChecks
{
    public sealed class Factory(DbContextOptions<ResearchDbContext> options) : IDbContextFactory<ResearchDbContext>
    { public ResearchDbContext CreateDbContext() => new(options); }

    public static Factory CreateFactory()
    {
        var name = Environment.GetEnvironmentVariable("M3_DATABASE") ?? "analysis_m3_checks";
        if (Environment.GetEnvironmentVariable("M3_ISOLATED_TEST") != "true" || name is not ("analysis_m3_checks" or "analysis_m2_checks"))
            throw new InvalidOperationException("Requires explicit isolated M3 verification database.");
        var connection = new NpgsqlConnectionStringBuilder { Host = "postgres", Database = name, Username = "analysis",
            Password = Environment.GetEnvironmentVariable("M3_DB_PASSWORD") ?? throw new InvalidOperationException("Missing isolated password"),
            Timeout = 3, CommandTimeout = 30, Timezone = "UTC", IncludeErrorDetail = false };
        return new(new DbContextOptionsBuilder<ResearchDbContext>().UseNpgsql(connection.ConnectionString,
            o => o.SetPostgresVersion(18, 0)).Options);
    }

    public static async Task RunAsync()
    {
        var factory = CreateFactory(); var model = ScoringModel.Slice1;
        await using (var db = factory.CreateDbContext())
        {
            Check.That(!(await db.Database.GetAppliedMigrationsAsync()).Any(), "Refuse destructive tests on an existing schema");
            await db.Database.MigrateAsync();
            Check.That(!db.Database.HasPendingModelChanges(), "No EF model drift");
            await db.GetService<IMigrator>().MigrateAsync("20260905210022_M2CatalogObservations");
        }
        var observationStore = new ObservationStore(factory);
        var window = new ReadWindow(Synthetic.T.AddDays(-8), Synthetic.T.AddHours(3));
        foreach (var instrument in CatalogSeed.Instruments)
        {
            var observations = Synthetic.Series(instrument).Where(o => o.Kind != ObservationKind.Candle || o.EventTimeUtc.AddHours(1) <= window.EndUtc).ToArray();
            await observationStore.SaveAsync(instrument, window,
                [new(new("/fixture/m3", "synthetic-m3-v1", Synthetic.Bytes(observations)), observations)], Synthetic.K, CancellationToken.None);
        }
        await observationStore.QuarantineAsync(CatalogSeed.Instruments.Single(i => i.Id == "binance:spot:BTCUSDT"),
            new(Synthetic.T.AddHours(-108), Synthetic.T.AddHours(-107)), "conflicting-observation", Synthetic.K, CancellationToken.None);
        var beforeMigration = await M2HashAsync(factory);
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            Check.Equal(2, (await db.Database.GetAppliedMigrationsAsync()).Count(), "M2 plus additive M3 migration only");
            Check.That(!db.Database.HasPendingModelChanges(), "Migrated model is current");
        }
        Check.Equal(beforeMigration, await M2HashAsync(factory), "Populated M2 facts/provenance/catalog preserved by upgrade");
        Check.Pass("Empty schema and populated M2 upgrade with exact M2 preservation");

        var store = new ScoringStore(factory); var jobs = new ScoringJobs(store, store, new Synthetic.Clock(Synthetic.K));
        var request = new ScoreRequest(Synthetic.T, Synthetic.K, "slice1-v1");
        var first = await jobs.RunAsync(request, CancellationToken.None);
        Check.That(!first.Duplicate && first.Bundle.Assets.All(a => a.Score.State == "complete"), "Three complete persisted asset scores");
        Check.Equal(1, first.Bundle.Input.Conflicts.Length, "Conflict capture retained for reproducibility");
        var beforeReplay = await SnapshotAsync();
        var second = await jobs.RunAsync(request, CancellationToken.None);
        Check.That(second.Duplicate && first.Id == second.Id, "Idempotent stored-input replay");
        var replay = await jobs.ReplayAsync("slice1-v1", Synthetic.T, Synthetic.T.AddHours(1), CancellationToken.None);
        Check.That(replay.Batches == 1 && replay.Scores == 3 && replay.MissingPeriods.Length == 0, "Exact complete replay range");
        Check.Equal(CanonicalJson.Write(beforeReplay), CanonicalJson.Write(await SnapshotAsync()), "Replay does not write anything");
        await Check.ThrowsAsync<ScoringPreconditionException>(() => jobs.RunAsync(request with { KnowledgeCutoffUtc = Synthetic.K.AddSeconds(-1) }, CancellationToken.None), "Existing key cannot refresh cutoff");
        await Check.ThrowsAsync<ScoringPreconditionException>(() => jobs.RunAsync(request with { ModelId = "slice1-v2-unregistered" }, CancellationToken.None), "Unregistered model cannot silently reuse calculator");
        var changedManifest = model.Manifest with { History = model.Manifest.History with { MinimumQuality = 51 } };
        Check.That(CanonicalJson.Hash(CanonicalJson.Write(changedManifest)) != CanonicalJson.Hash(CanonicalJson.Write(model.Manifest)), "Policy change has different manifest identity");
        Check.Pass("Persisted exact feature/model lineage, idempotence, local replay and version/cutoff conflicts");

        var concurrentRequest = request with { AsOfUtc = Synthetic.T.AddHours(1) };
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => jobs.RunAsync(concurrentRequest, CancellationToken.None)));
        Check.Equal(1, concurrent.Count(r => !r.Duplicate), "Four concurrent writers publish once");
        Check.Equal(1, concurrent.Select(r => r.Id).Distinct().Count(), "Concurrent writers use the winning snapshot");
        var priorInput = CanonicalJson.Write(first.Bundle.Input);
        var chain = CatalogSeed.Instruments.Single(i => i.Id == "defillama:chain:Ethereum");
        var lateFact = new Observation(chain.Id, ObservationKind.ChainTvl, Synthetic.T.AddHours(-1), 0, "USD", null, Value: 2_000_000);
        await observationStore.SaveAsync(chain, new(Synthetic.T.AddHours(-2), Synthetic.T),
            [new(new("/fixture/m3-late", "synthetic-m3-v1", Synthetic.Bytes([lateFact])), [lateFact])], Synthetic.K.AddMilliseconds(1), CancellationToken.None);
        var afterLate = await jobs.RunAsync(request, CancellationToken.None);
        Check.Equal(priorInput, CanonicalJson.Write(afterLate.Bundle.Input), "Late arrival cannot alter existing snapshot");
        var captured = await store.CaptureAsync(request, model, CancellationToken.None);
        Check.That(!captured.Observations.Any(f => f.Observation == lateFact), "Late ingested-at cutoff enforced in real database read");
        Check.Pass("Concurrent publication and late-observation/cutoff isolation");

        var immutableBefore = await SnapshotAsync();
        foreach (var table in new[] { "ScoringModels", "ScoringBatches", "InputObservations", "InputConflicts", "FeatureSnapshots", "FeatureValues", "ScoreSnapshots", "CategoryScores" })
        {
            var column = table switch { "InputObservations" or "InputConflicts" => "FactJson", "FeatureValues" => "DetailJson", "CategoryScores" => "State", _ => "Id" };
            foreach (var sql in new[] { $"UPDATE research.\"{table}\" SET \"{column}\" = \"{column}\"", $"DELETE FROM research.\"{table}\"", $"TRUNCATE research.\"{table}\" CASCADE" })
            {
                await using var db = factory.CreateDbContext();
                await Check.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql), "Immutable SQL operation rejected");
            }
        }
        await using (var db = factory.CreateDbContext())
        {
            var extra = await db.Observations.FirstAsync(o => o.EventTimeUtc < Synthetic.T.AddHours(-150));
            db.Add(new InputObservationRow { BatchId = first.Id, InstrumentId = extra.InstrumentId, Kind = extra.Kind,
                EventTimeUtc = extra.EventTimeUtc, PeriodSeconds = extra.PeriodSeconds, FactJson = "{}" });
            await Check.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(), "Cannot insert later snapshot child");
        }
        await using (var db = factory.CreateDbContext())
        {
            db.Add(new ScoringModelRow { Id = "slice1-v1", ManifestJson = "{}", ManifestHash = new('0',64), SourceHash = new('0',64), CreatedAtUtc = Synthetic.K });
            await Check.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(), "Changed manifest cannot reuse primary version identity");
        }
        Check.Equal(CanonicalJson.Write(immutableBefore), CanonicalJson.Write(await SnapshotAsync()), "All rejected writes preserve entire database snapshot");
        await using (var db = factory.CreateDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Add(EmptyBatch("incomplete-fixture", Synthetic.T.AddHours(4), model));
            await db.SaveChangesAsync();
            await Check.ThrowsAsync<PostgresException>(() => transaction.CommitAsync(), "Incomplete bundle rejected at commit");
        }
        await using (var db = factory.CreateDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            db.Add(EmptyBatch("bad-lineage-fixture", Synthetic.T.AddHours(4), model));
            await db.SaveChangesAsync();
            db.Add(new InputObservationRow { BatchId = "bad-lineage-fixture", InstrumentId = "binance:spot:BTCUSDT",
                Kind = ObservationKind.Candle, EventTimeUtc = Synthetic.T.AddDays(300), PeriodSeconds = 3600, FactJson = "{}" });
            try { await db.SaveChangesAsync(); throw new InvalidOperationException("Invalid observation FK accepted"); }
            catch (DbUpdateException e) { Check.Equal("23503", (e.InnerException as PostgresException)?.SqlState, "Actual observation lineage FK rejection"); }
        }
        Check.Pass("Database UPDATE/DELETE/TRUNCATE protection, sealed children, version uniqueness, complete-bundle and lineage guards");

        await using (var db = factory.CreateDbContext())
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            var asOf = Synthetic.T.AddHours(4);
            db.Add(EmptyBatch("asset-lineage-fixture", asOf, model));
            await db.SaveChangesAsync();
            db.Add(new FeatureSnapshotRow { Id = "asset-feature-fixture", BatchId = "asset-lineage-fixture", AssetId = "bitcoin",
                AsOfUtc = asOf, ModelId = "slice1-v1", CorePriceReady = true, FeatureHash = model.Hash });
            await db.SaveChangesAsync();
            db.Add(new ScoreSnapshotRow { Id = "asset-score-fixture", SnapshotId = "asset-feature-fixture", BatchId = "asset-lineage-fixture",
                AssetId = "ethereum", AsOfUtc = asOf, ModelId = "slice1-v1", State = "complete", Composite = 0,
                BullishConfidence = 0, BearishConfidence = 0, DataQuality = 100, ContextCoverage = 100, ScoreJson = "{}", ScoreHash = model.Hash });
            try { await db.SaveChangesAsync(); throw new InvalidOperationException("Cross-asset feature lineage accepted"); }
            catch (DbUpdateException e) { Check.Equal("23503", (e.InnerException as PostgresException)?.SqlState, "Score cannot reference another asset's feature snapshot"); }
        }

        var cancelRequest = request with { AsOfUtc = Synthetic.T.AddHours(2) };
        var cancelBefore = await SnapshotAsync();
        await using (var blocking = factory.CreateDbContext())
        {
            await using var transaction = await blocking.Database.BeginTransactionAsync();
            await blocking.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({ScoringStore.LockKey("slice1-v1", cancelRequest.AsOfUtc)}, 0))");
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(350));
            await Check.ThrowsAsync<OperationCanceledException>(() => jobs.RunAsync(cancelRequest, cancellation.Token), "Cancellation interrupts blocked score write");
        }
        Check.Equal(CanonicalJson.Write(cancelBefore), CanonicalJson.Write(await SnapshotAsync()), "Cancelled publication leaves no partial bundle");
        var newM2Hash = await M2HashAsync(factory);
        await using (var db = factory.CreateDbContext())
        {
            await db.GetService<IMigrator>().MigrateAsync("20260905210022_M2CatalogObservations");
            Check.Equal(newM2Hash, await M2HashAsync(factory), "Disposable M3 rollback preserves M2");
            await db.Database.MigrateAsync();
        }
        await jobs.RunAsync(request, CancellationToken.None);
        await jobs.RunAsync(concurrentRequest, CancellationToken.None);
        Check.Pass("Blocked-write cancellation, atomic rollback and disposable M3 down/reapply preserving M2");
    }

    private static ScoringBatchRow EmptyBatch(string id, DateTimeOffset asOf, ScoringModel model) => new()
    {
        Id = id, ModelId = "slice1-v1", AsOfUtc = asOf, KnowledgeCutoffUtc = Synthetic.K, CreatedAtUtc = Synthetic.K,
        RecordKind = "research-reconstruction", UniverseJson = CanonicalJson.Write(model.Manifest.Universe), InputJson = "{}", InputHash = new('0',64)
    };

    public static async Task<object> SnapshotAsync()
    {
        var factory = CreateFactory(); await using var db = factory.CreateDbContext();
        var models = await db.Set<ScoringModelRow>().AsNoTracking().OrderBy(r => r.Id).ToArrayAsync();
        var batches = await db.Set<ScoringBatchRow>().AsNoTracking().OrderBy(r => r.Id).ToArrayAsync();
        var inputObservations = await db.Set<InputObservationRow>().AsNoTracking().OrderBy(r => r.BatchId).ThenBy(r => r.InstrumentId).ThenBy(r => r.Kind).ThenBy(r => r.EventTimeUtc).ThenBy(r => r.PeriodSeconds).ToArrayAsync();
        var conflicts = await db.Set<InputConflictRow>().AsNoTracking().OrderBy(r => r.BatchId).ThenBy(r => r.ConflictId).ToArrayAsync();
        var snapshots = await db.Set<FeatureSnapshotRow>().AsNoTracking().OrderBy(r => r.Id).ToArrayAsync();
        var features = await db.Set<FeatureValueRow>().AsNoTracking().OrderBy(r => r.SnapshotId).ThenBy(r => r.FeatureId).ToArrayAsync();
        var scores = await db.Set<ScoreSnapshotRow>().AsNoTracking().OrderBy(r => r.Id).ToArrayAsync();
        var categories = await db.Set<CategoryScoreRow>().AsNoTracking().OrderBy(r => r.ScoreId).ThenBy(r => r.Category).ToArrayAsync();
        return new
        {
            m2Hash = await M2HashAsync(factory),
            scoringHash = CanonicalJson.Hash(CanonicalJson.Write(new { models, batches, inputObservations, conflicts, snapshots, features, scores, categories })),
            batches = batches.Length, scores = scores.Length, ready = scores.Count(s => s.State != "not-ready"),
            complete = scores.Count(s => s.State == "complete"), features = features.Length,
            unusableApplicableFeatures = features.Count(f => f.State is not ("available" or "inapplicable")),
            featureStates = features.GroupBy(f => new { asset = snapshots.Single(s => s.Id == f.SnapshotId).AssetId, f.FeatureId, f.State })
                .OrderBy(g => g.Key.asset, StringComparer.Ordinal).ThenBy(g => g.Key.FeatureId).ThenBy(g => g.Key.State, StringComparer.Ordinal)
                .Select(g => new { g.Key.asset, g.Key.FeatureId, g.Key.State, count = g.Count() }).ToArray(),
            manifestHash = models.SingleOrDefault()?.ManifestHash, sourceHash = models.SingleOrDefault()?.SourceHash
        };
    }
    private static async Task<string> M2HashAsync(Factory factory)
    {
        await using var db = factory.CreateDbContext();
        var assets = await db.Assets.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync();
        var providers = await db.Providers.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync();
        var instruments = await db.Instruments.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync();
        var payloads = await db.Payloads.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync();
        var observations = await db.Observations.AsNoTracking().OrderBy(r => r.InstrumentId).ThenBy(r => r.Kind).ThenBy(r => r.EventTimeUtc).ThenBy(r => r.PeriodSeconds).ToArrayAsync();
        var quarantine = await db.Quarantine.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync();
        return CanonicalJson.Hash(CanonicalJson.Write(new { assets, providers, instruments, payloads, observations, quarantine }));
    }

    public static async Task HoldLockAsync()
    {
        var factory = CreateFactory(); await using var db = factory.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({ScoringStore.LockKey("slice1-v1", Synthetic.T.AddHours(2))}, 0))");
        Console.WriteLine(JsonSerializer.Serialize(new { status = "locked" }));
        // Test harness removes this task-owned helper after checking worker SIGTERM.
        await Task.Delay(TimeSpan.FromMinutes(2));
    }
}
