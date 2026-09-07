using System.Text.Json;
using System.Data.Common;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Analysis.Domain.SignalsOutcomes;
using Analysis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Analysis.ForwardChecks;

internal static class DatabaseChecks
{
    internal sealed class Factory(DbContextOptions<ResearchDbContext> options) : IDbContextFactory<ResearchDbContext>
    { public ResearchDbContext CreateDbContext() => new(options); }
    internal sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    internal static Factory CreateFactory()
    {
        if (Environment.GetEnvironmentVariable("FORWARD_ISOLATED_TEST") != "true") throw new InvalidOperationException("Explicit disposable 1A test environment required.");
        var host = Environment.GetEnvironmentVariable("FORWARD_DB_HOST") ?? "postgres";
        var port = 5432;
        if (host != "postgres" && (host != "127.0.0.1" || !int.TryParse(Environment.GetEnvironmentVariable("FORWARD_DB_PORT"), out port) || port < 1024 || port > 65535))
            throw new InvalidOperationException("Only isolated Compose or explicit loopback test runtime permitted.");
        var connection = new NpgsqlConnectionStringBuilder { Host = host, Port = port, Database = "analysis_1a_checks", Username = "analysis",
            Password = Environment.GetEnvironmentVariable("FORWARD_DB_PASSWORD") ?? throw new InvalidOperationException("Missing disposable password"),
            Timeout = 3, CommandTimeout = 30, Timezone = "UTC" };
        return new(new DbContextOptionsBuilder<ResearchDbContext>().UseNpgsql(connection.ConnectionString, o => o.SetPostgresVersion(18, 0)).Options);
    }
    private static async Task SetClock(Factory factory, DateTimeOffset time)
    {
        Utc.Require(time);
        await using var db = factory.CreateDbContext();
        // Trusted synthetic instant only, in the explicitly isolated database; no production clock override.
        var literal = time.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var sql = "CREATE OR REPLACE FUNCTION research.\"ForwardClock\"() RETURNS timestamptz LANGUAGE sql VOLATILE AS $$ SELECT '" + literal + "'::timestamptz $$";
        await db.Database.ExecuteSqlRawAsync(sql);
    }
    private static async Task Seed(ForwardObservationStore store, ForwardCollectionWork work, Observation[] observations) =>
        _ = await store.SaveAsync(work, [new(new("/fixture/forward", "synthetic-1a-v1", Synthetic.Bytes(observations)), observations)], CancellationToken.None);
    public static async Task RunAsync()
    {
        var factory = CreateFactory(); var t = Synthetic.T;
        await using (var db = factory.CreateDbContext())
        {
            Check.That(!(await db.Database.GetAppliedMigrationsAsync()).Any(), "Refuse destructive checks on existing schema");
            await db.GetService<IMigrator>().MigrateAsync("20260906143029_M3FeaturesScores");
        }
        var legacy = new ObservationStore(factory); var oldT = t.AddHours(-24); var oldK = oldT.AddHours(1);
        foreach (var i in CatalogSeed.Instruments)
        {
            var window = new ReadWindow(t.AddHours(-240), oldT);
            var observations = Synthetic.Series(i, window.StartUtc, window.EndUtc);
            await legacy.SaveAsync(i, window, [new(new("/fixture/pre-1a", "synthetic-1a-v1", Synthetic.Bytes(observations)), observations)], oldK, CancellationToken.None);
        }
        var scoring = new ScoringStore(factory);
        // The current writer requires the current schema. Remove the empty 1A schema
        // after preparing representative M3 records, then exercise their upgrade.
        await using (var db = factory.CreateDbContext()) await db.Database.MigrateAsync();
        await new ScoringJobs(scoring, scoring, new Clock(oldK)).RunAsync(new(oldT, oldK, "slice1-v1"), CancellationToken.None);
        await using (var db = factory.CreateDbContext()) await db.GetService<IMigrator>().MigrateAsync("20260906143029_M3FeaturesScores");
        var oldHash = await OldHashAsync(factory);
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            Check.That(!db.Database.HasPendingModelChanges(), "EF schema matches generated model");
            await db.GetService<IMigrator>().MigrateAsync("20260906143029_M3FeaturesScores");
            await db.Database.MigrateAsync();
        }
        Check.Equal(oldHash, await OldHashAsync(factory), "Populated M3 upgrade and disposable rollback preserve old facts/scores");
        await SetClock(factory, Synthetic.K);
        var collector = new ForwardObservationStore(factory, legacy);
        var work = await collector.PlanInputsAsync(t, CancellationToken.None);
        Check.Equal(8, work.Length, "Exact catalog collection");
        Check.That(work.Where(w => w.Instrument.Kind == InstrumentKind.Spot).All(w => w.Window.EndUtc == t), "Only closed input candles");
        Check.That(work.Where(w => w.Instrument.Kind != InstrumentKind.Spot).All(w => w.Window.EndUtc == t.AddMilliseconds(1)), "Exact T scalar included");
        foreach (var item in work) await Seed(collector, item, Synthetic.Series(item.Instrument, item.Window.StartUtc, item.Window.EndUtc));
        var store = new ForwardStore(factory); var jobs = new ForwardRecordingJobs(store);
        var writers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => jobs.IssueAsync("slice1-v1", t, CancellationToken.None)));
        Check.Equal(1, writers.Count(w => !w.Duplicate), "Four writers publish exactly one bundle and issuance");
        Check.Equal(1, writers.Select(w => w.Original.Id).Distinct().Count(), "Concurrent losers return original identity");
        var original = writers[0].Original;
        Check.Equal(3, original.EligibleAssetIds.Length, "All known inputs ready");
        Check.Equal("research-reconstruction", (await new RankingsReader(factory).ReadAsync(new("slice1-v1", t), CancellationToken.None)).RecordKind, "Underlying M3 label preserved");
        await SetClock(factory, t.AddMinutes(16));
        Check.That((await jobs.IssueAsync("slice1-v1", t, CancellationToken.None)).Duplicate, "Old successful issue remains retryable without refreshing");
        await Check.ThrowsAsync<ArgumentException>(() => jobs.IssueAsync("slice1-v1", t.AddHours(-1), CancellationToken.None), "Historical new issuance refused");
        await SetClock(factory, t.AddHours(1).AddMinutes(2));
        await new ScoringJobs(scoring, scoring, new Clock(t.AddHours(1).AddMinutes(2))).RunAsync(new(t.AddHours(1), t.AddHours(1).AddMinutes(2), "slice1-v1"), CancellationToken.None);
        await Check.ThrowsAsync<ForwardPreconditionException>(() => jobs.IssueAsync("slice1-v1", t.AddHours(1), CancellationToken.None), "Standalone reconstruction cannot become forward issued");
        await SetClock(factory, t.AddHours(2).AddMinutes(2));
        var notReady = await jobs.IssueAsync("slice1-v1", t.AddHours(2), CancellationToken.None);
        Check.Equal(0, notReady.Original.EligibleAssetIds.Length, "All-not-ready issuance persisted");
        var initial = await store.MeasureAsync(original.Id, CancellationToken.None);
        Check.Equal(12, initial, "All initial outcome states stored");
        Check.Equal(0, await store.MeasureAsync(original.Id, CancellationToken.None), "Unchanged assessments idempotent");
        await using (var db = factory.CreateDbContext())
        {
            Check.Equal(3, await db.Set<ForwardOutcomeAssessmentRow>().CountAsync(a => a.IssuanceId == original.Id && a.State == "incomplete"), "Mature missing one-hour outcomes incomplete");
            Check.Equal(9, await db.Set<ForwardOutcomeAssessmentRow>().CountAsync(a => a.IssuanceId == original.Id && a.State == "pending"), "Longer horizons pending");
        }
        await SetClock(factory, t.AddHours(170));
        foreach (var window in new[] { new ReadWindow(t, t.AddHours(168)), new ReadWindow(t.AddHours(168), t.AddHours(169)) })
        {
            var outcomeWork = await collector.PlanOutcomesAsync(window, CancellationToken.None);
            Check.That(outcomeWork.All(w => w.Instrument.ProviderId == "binance"), "Outcome collection never creates derivative/fundamental work");
            foreach (var item in outcomeWork)
            {
                var values = Synthetic.Series(item.Instrument, item.Window.StartUtc, item.Window.EndUtc).Select(o =>
                {
                    var hour = (decimal)(o.EventTimeUtc - t).TotalHours;
                    var value = item.Instrument.AssetId == "bitcoin" ? 100m + hour : item.Instrument.AssetId == "ethereum" ? 100m - hour / 10m : 100m;
                    return o with { Open = value, High = value, Low = value, Close = value };
                }).ToArray();
                await Seed(collector, item, values);
            }
        }
        var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => store.MeasureAsync(original.Id, CancellationToken.None)));
        Check.Equal(12, concurrent.Sum(), "Concurrent matured assessments have one winner per evidence");
        var reader = new ForwardRecordReader(factory); var range = new ForwardRange(t, t.AddHours(1));
        var beforeInspect = await SnapshotAsync();
        var report = await reader.InspectAsync(range, CancellationToken.None);
        Check.Equal(24, report.Records.Single().History.Length, "Pending/incomplete history retained beside completed outcomes");
        Check.Equal(12, report.Records.Single().Outcomes.Count(o => o.Result.State == "complete"), "All four horizons completed");
        Check.Equal<decimal?>(-0.168m, report.Records.Single().Outcomes.Single(o => o.Result.Target.AssetId == "ethereum" && o.Result.Target.HorizonHours == 168).Result.Return, "Unfavorable seven-day return retained");
        Check.That(report.Aggregates.All(a => a.PairedCompleteSamples == 1 && a.ModelMinusBitcoinMean == 0), "Matched benchmark arithmetic");
        Check.Equal(beforeInspect, await SnapshotAsync(), "Inspection/replay read-only");
        Check.Equal(0, await store.MeasureAsync(original.Id, CancellationToken.None), "Completed duplicate no-op");
        var btc = CatalogSeed.Instruments.Single(i => i.Id == "binance:spot:BTCUSDT");
        await SetClock(factory, t.AddHours(172));
        var mixedWork = new ForwardCollectionWork(btc, new(t.AddHours(167), t.AddHours(171)));
        var candidates = new[] { Synthetic.Candle(btc, t.AddHours(168), 269), Synthetic.Candle(btc, t.AddHours(170), 270) };
        var mixed = await collector.SaveAsync(mixedWork, [new(new("/fixture/revision", "synthetic-1a-v1", Synthetic.Bytes(candidates)), candidates)], CancellationToken.None);
        Check.That(mixed.Inserted == 1 && mixed.Conflicts == 1, "Revision does not roll back unrelated valid observation");
        Check.Equal(1, await store.MeasureAsync(original.Id, CancellationToken.None), "Revision appends one affected horizon assessment");
        var disputed = await reader.InspectAsync(range, CancellationToken.None);
        var result168 = disputed.Records.Single().Outcomes.Single(o => o.Result.Target.AssetId == "bitcoin" && o.Result.Target.HorizonHours == 168);
        Check.That(result168.Result.State == "conflicted" && result168.Result.Return == 1.68m && result168.Result.OriginalCompleteAssessmentId is not null, "Original terminal price/return preserved after revision");
        Check.Equal(0, disputed.Aggregates.Single(a => a.HorizonHours == 168).PairedCompleteSamples, "Disputed paired sample disclosed and excluded");
        Check.Equal(CanonicalJson.Write(original), CanonicalJson.Write(disputed.Records.Single().Original), "Outcome acquisition never revises original ranking");
        await SetClock(factory, t.AddHours(172).AddMinutes(1));
        var laterCandidate = new[] { Synthetic.Candle(btc, t.AddHours(168), 271) };
        await Seed(collector, mixedWork, laterCandidate);
        Check.Equal(1, await store.MeasureAsync(original.Id, CancellationToken.None), "Another revision to the same candle appends distinct evidence");
        var later = (await reader.InspectAsync(range, CancellationToken.None)).Records.Single();
        var later168 = later.Outcomes.Single(o => o.Result.Target.AssetId == "bitcoin" && o.Result.Target.HorizonHours == 168);
        Check.That(later168.Result.Evidence.Revisions.Length == 2 && later168.Result.Return == 1.68m, "Both rejected candidates retained beside original numeric result");
        Check.Equal(0, await store.MeasureAsync(original.Id, CancellationToken.None), "Repeated revision evidence no-op");
        await GuardsAsync(factory, original);
        await CancellationAsync(factory, jobs, t.AddHours(173));
        await RollbackAndPagingAsync(factory, jobs, store, reader, t);
        await EqualCutoffRaceAsync(factory, store, reader, collector, t);
        Check.Pass("Populated migration, atomic issuance, all-horizon history/replay, revisions, concurrency, SQL guards and cancellation");
    }
    private static async Task GuardsAsync(Factory factory, ForwardOriginal original)
    {
        await using (var db = factory.CreateDbContext())
        {
            db.Add(new ForwardRunEventRow { Id = "synthetic-guard-event", RunId = "synthetic-guard", Operation = "synthetic-test",
                State = "started", AtUtc = Synthetic.T.AddHours(172), StartUtc = Synthetic.T, EndUtc = Synthetic.T.AddHours(1), DetailJson = "{}" });
            await db.SaveChangesAsync();
        }
        var before = await SnapshotAsync();
        foreach (var table in new[] { "ForwardMethodologies", "ForwardIssuances", "ForwardIssuanceItems", "ForwardOutcomeTargets",
            "ForwardOutcomeAssessments", "ForwardOutcomeInputs", "ForwardObservationConflicts", "ForwardRunEvents" })
        {
            await using var db = factory.CreateDbContext();
            foreach (var sql in new[] { $"DELETE FROM research.\"{table}\"", $"TRUNCATE research.\"{table}\" CASCADE" })
                await Check.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync(sql), "Immutable SQL refusal " + table);
        }
        await using (var db = factory.CreateDbContext())
        {
            await Check.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlRawAsync("UPDATE research.\"ForwardIssuances\" SET \"OriginalHash\" = 'changed'"), "Update rejected");
            db.Add(new ForwardIssuanceItemRow { IssuanceId = original.Id, BatchId = original.BatchId, AssetId = "not-an-asset", ScoreSnapshotId = original.Items[0].ScoreSnapshotId, ItemJson = "{}" });
            await Check.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(), "Sealed issuance cannot accept child");
        }
        Check.Equal(before, await SnapshotAsync(), "Rejected writes preserve all tables");
    }
    private static async Task RollbackAndPagingAsync(Factory factory, ForwardRecordingJobs jobs, ForwardStore store, ForwardRecordReader reader, DateTimeOffset t)
    {
        var failedT = t.AddHours(176); await SetClock(factory, failedT.AddMinutes(2));
        await using (var db = factory.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync("""
                CREATE FUNCTION research."TestFailTarget"() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'synthetic-target-failure'; END $$;
                CREATE TRIGGER "TestFailTarget" BEFORE INSERT ON research."ForwardOutcomeTargets" FOR EACH ROW EXECUTE FUNCTION research."TestFailTarget"();
                """);
        await Check.ThrowsAsync<DbUpdateException>(() => jobs.IssueAsync("slice1-v1", failedT, CancellationToken.None), "Late child write failure rolls back entire issuance transaction");
        await using (var db = factory.CreateDbContext())
        {
            Check.That(!await db.Set<ScoringBatchRow>().AnyAsync(b => b.AsOfUtc == failedT) && !await db.Set<ForwardIssuanceRow>().AnyAsync(i => i.AsOfUtc == failedT), "No orphan scoring batch after child failure");
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER \"TestFailTarget\" ON research.\"ForwardOutcomeTargets\"; DROP FUNCTION research.\"TestFailTarget\"();");
        }
        Check.That(!(await jobs.IssueAsync("slice1-v1", failedT, CancellationToken.None)).Duplicate, "Same logical request recovers after rollback");
        for (var n = 180; n < 206; n++)
        { await SetClock(factory, t.AddHours(n).AddMinutes(2)); await jobs.IssueAsync("slice1-v1", t.AddHours(n), CancellationToken.None); }
        var range = new ForwardRange(t.AddHours(180), t.AddHours(206));
        var first = await store.ReadPageAsync(range, CancellationToken.None);
        Check.That(first.Items.Length == 24 && first.NextCursor is not null, "Measurement page has fixed bound and explicit next cursor");
        var second = await store.ReadPageAsync(range with { AfterId = first.NextCursor }, CancellationToken.None);
        Check.That(second.Items.Length == 2 && second.NextCursor is null && !first.Items.Select(i => i.Id).Intersect(second.Items.Select(i => i.Id)).Any(), "Cursor continuation omits no rows and repeats none");
        await Check.ThrowsAsync<ForwardPreconditionException>(() => store.ReadPageAsync(range with { AfterId = "missing" }, CancellationToken.None), "Unknown cursor fails closed");
        var all = await reader.InspectAsync(range, CancellationToken.None);
        Check.That(all.Records.Length == 26 && all.Aggregates.All(a => a.TotalIssuances == 26 && a.PairedCompleteSamples == 0), "Inspection aggregation includes entire bounded interval, not one job page");
        Check.Equal(0, all.UnissuedHours, "Open current issuance window does not count as missed");
        var partial = await reader.InspectAsync(new(t.AddHours(180).AddMinutes(3), t.AddHours(181).AddMinutes(1)), CancellationToken.None);
        Check.Equal(0, partial.UnissuedHours, "Partially selected hours do not create false missed-issuance counts");
    }
    private sealed class PublicationGate : DbCommandInterceptor
    {
        public TaskCompletionSource Captured { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal))
            { Captured.TrySetResult(); await Release.Task.WaitAsync(cancellationToken); }
            return result;
        }
    }
    private static async Task EqualCutoffRaceAsync(Factory factory, ForwardStore store, ForwardRecordReader reader, ForwardObservationStore collector, DateTimeOffset t)
    {
        var issueT = t.AddHours(176); var issue = (await store.FindAsync("slice1-v1", issueT, CancellationToken.None))!;
        var instrument = CatalogSeed.Instruments.Single(i => i.Id == "binance:spot:BTCUSDT");
        var work = new ForwardCollectionWork(instrument, new(issueT, issueT.AddHours(1)));
        await Seed(collector, work, [Synthetic.Candle(instrument, issueT)]);
        var gate = new PublicationGate();
        await using var db = factory.CreateDbContext();
        var gatedFactory = new Factory(new DbContextOptionsBuilder<ResearchDbContext>()
            .UseNpgsql(db.Database.GetConnectionString(), o => o.SetPostgresVersion(18, 0)).AddInterceptors(gate).Options);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var stale = new ForwardStore(gatedFactory).MeasureAsync(issue.Id, deadline.Token);
        try
        {
            await gate.Captured.Task.WaitAsync(deadline.Token);
            // Clock stays fixed while another committed capture observes a revision.
            await Seed(collector, work, [Synthetic.Candle(instrument, issueT, 101)]);
            Check.Equal(12, await store.MeasureAsync(issue.Id, deadline.Token), "Newer evidence wins before delayed capture with equal K");
        }
        finally { gate.Release.TrySetResult(); }
        Check.Equal(0, await stale, "Equal-cutoff stale writer cannot erase known revision evidence");
        var record = (await reader.InspectAsync(new(issueT, issueT.AddHours(1)), CancellationToken.None)).Records.Single();
        Check.That(record.Outcomes.Where(o => o.Result.Target.AssetId == "bitcoin" && o.Result.Target.HorizonHours < 168).All(o => o.Result.State == "conflicted"), "Mature incomplete outcomes cannot auto-resolve conflict under concurrency");
        Check.Equal(12, record.History.Length, "Race preserves only distinct non-regressing evidence states");
    }
    private static async Task CancellationAsync(Factory factory, ForwardRecordingJobs jobs, DateTimeOffset t)
    {
        await SetClock(factory, t.AddMinutes(2));
        await using var blocker = factory.CreateDbContext();
        await using var tx = await blocker.Database.BeginTransactionAsync();
        await blocker.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({ScoringStore.LockKey("slice1-v1", t)}, 0))");
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        await Check.ThrowsAsync<OperationCanceledException>(() => jobs.IssueAsync("slice1-v1", t, cancellation.Token), "Cancel actual blocked issuance");
        await using var verify = factory.CreateDbContext();
        Check.That(!await verify.Set<ScoringBatchRow>().AnyAsync(b => b.AsOfUtc == t) && !await verify.Set<ForwardIssuanceRow>().AnyAsync(i => i.AsOfUtc == t), "Cancelled publication has neither bundle nor issuance");
    }
    private static async Task<string> OldHashAsync(Factory factory)
    {
        await using var db = factory.CreateDbContext();
        return CanonicalJson.Hash(CanonicalJson.Write(new { observations = await db.Observations.AsNoTracking().OrderBy(o => o.InstrumentId).ThenBy(o => o.EventTimeUtc).ThenBy(o => o.Kind).ToArrayAsync(),
            scores = await db.Set<ScoreSnapshotRow>().AsNoTracking().OrderBy(s => s.Id).ToArrayAsync(), batches = await db.Set<ScoringBatchRow>().AsNoTracking().OrderBy(s => s.Id).ToArrayAsync() }));
    }
    public static async Task<string> SnapshotAsync()
    {
        var factory = CreateFactory(); await using var db = factory.CreateDbContext();
        var contents = new List<string>();
        foreach (var table in new[] { "Assets", "ProviderInstrumentRefs", "ProviderPayloads", "Observations", "Quarantine", "ScoringModels", "ScoringBatches", "ScoreSnapshots",
            "FeatureSnapshots", "FeatureValues", "CategoryScores", "InputObservations", "InputConflicts", "ForwardMethodologies", "ForwardIssuances", "ForwardIssuanceItems", "ForwardOutcomeTargets",
            "ForwardOutcomeAssessments", "ForwardOutcomeInputs", "ForwardObservationConflicts", "ForwardRunEvents" })
        {
            var sql = "SELECT COALESCE(jsonb_agg(j ORDER BY j::text), '[]'::jsonb)::text AS \"Value\" FROM (SELECT to_jsonb(t) j FROM research.\"" + table + "\" t) s";
            contents.Add(await db.Database.SqlQueryRaw<string>(sql).SingleAsync());
        }
        return CanonicalJson.Hash(CanonicalJson.Write(contents));
    }
    public static async Task HoldIssuanceLockAsync()
    {
        var factory = CreateFactory(); await SetClock(factory, Synthetic.T.AddHours(220).AddMinutes(2));
        await using var db = factory.CreateDbContext(); await using var tx = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"m3-model:slice1-v1"}, 0))");
        Console.WriteLine("{\"held\":true}");
        await Task.Delay(TimeSpan.FromSeconds(60));
    }
    public static async Task CheckSignalCancellationAsync()
    {
        await using var db = CreateFactory().CreateDbContext(); var t = Synthetic.T.AddHours(220);
        Check.That(!await db.Set<ScoringBatchRow>().AnyAsync(b => b.AsOfUtc == t) && !await db.Set<ForwardIssuanceRow>().AnyAsync(i => i.AsOfUtc == t), "SIGTERM publication leaves no scoring or issuance rows");
        Check.That(await db.Set<ForwardRunEventRow>().AnyAsync(e => e.State == "cancelled" && e.StartUtc == t), "SIGTERM cancellation audited");
    }
}
