using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using Analysis.Api.Rankings;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Analysis.Infrastructure.Persistence;
using Analysis.ScoringChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Analysis.RankingsChecks;

internal static class DatabaseChecks
{
    private sealed class Factory(DbContextOptions<ResearchDbContext> options) : IDbContextFactory<ResearchDbContext>
    { public ResearchDbContext CreateDbContext() => new(options); }
    public static async Task Run()
    {
        if (Environment.GetEnvironmentVariable("M4_ISOLATED_TEST") != "true") throw new InvalidOperationException("Explicit isolated verification required");
        var connection = new NpgsqlConnectionStringBuilder { Host = "postgres", Database = "analysis_m4_checks", Username = "analysis",
            Password = Environment.GetEnvironmentVariable("M4_DB_PASSWORD") ?? throw new InvalidOperationException("Missing isolated password"),
            CommandTimeout = 5, Timeout = 3, Timezone = "UTC" };
        var factory = new Factory(new DbContextOptionsBuilder<ResearchDbContext>().UseNpgsql(connection.ConnectionString).Options);
        var audit = new ReadAudit();
        var readFactory = new Factory(new DbContextOptionsBuilder<ResearchDbContext>().UseNpgsql(connection.ConnectionString).AddInterceptors(audit).Options);
        var reader = new RankingsReader(readFactory);
        var request = new RankingsRequest("slice1-v1", null);
        await using (var db = factory.CreateDbContext())
        {
            Program.Check(!(await db.Database.GetAppliedMigrationsAsync()).Any(), "Refuse test setup on existing schema");
            await Expect("schema-not-ready", () => reader.ReadAsync(request, default));
            await db.Database.MigrateAsync();
            Program.Check(!db.Database.HasPendingModelChanges(), "M4 adds no migration/model drift");
        }
        await Expect("model-not-found", () => reader.ReadAsync(request, default));
        var store = new ScoringStore(factory);
        var observations = new ObservationStore(factory);
        foreach (var instrument in CatalogSeed.Instruments)
        {
            var series = Synthetic.Series(instrument).ToArray();
            await observations.SaveAsync(instrument, new(Synthetic.T.AddDays(-8), Synthetic.T.AddHours(3)),
                [new(new("/fixture/m4", "synthetic-m4-v1", Synthetic.Bytes(series)), series)], Synthetic.K, default);
        }
        async Task<StoredScoringBatch> Publish(int hour, int createdHour = 0)
        {
            var input = await store.CaptureAsync(new(Synthetic.T.AddHours(hour), Synthetic.K, "slice1-v1"), ScoringModel.Slice1, default);
            return await store.PublishAsync(ScoringJobs.Calculate(input, ScoringModel.Slice1), ScoringModel.Slice1, Synthetic.K.AddHours(createdHour), default);
        }
        await Publish(2); await Publish(0, 2); await Publish(1, 1);
        var latest = await reader.ReadAsync(request, default);
        Program.Check(latest.AsOfUtc == Synthetic.T.AddHours(2) && latest.CreatedAtUtc == Synthetic.K, "Latest sorts as-of, not creation order");
        var exact = await reader.ReadAsync(request with { AsOfUtc = Synthetic.T }, default);
        Program.Check(exact.Items.Length == 3 && exact.KnowledgeCutoffUtc == Synthetic.K, "Exact whole batch/cutoff");
        await Expect("batch-not-found", () => reader.ReadAsync(request with { AsOfUtc = Synthetic.T.AddHours(-1) }, default));
        await Expect("model-not-found", () => reader.ReadAsync(request with { ModelId = "absent-model" }, default));
        await Publish(3); await Publish(4);
        var partial = await reader.ReadAsync(request with { AsOfUtc = Synthetic.T.AddHours(3) }, default);
        Program.Check(partial.Items.All(i => i.Score.State == "partial"), "Persisted incomplete OI context gives qualified partial scores");
        var unready = await reader.ReadAsync(request, default);
        Program.Check(unready.AsOfUtc == Synthetic.T.AddHours(4) && unready.Items.All(i => i.Score.State == "not-ready"), "Latest never falls back to older ready scores");
        var mapped = RankingTransport.Map(request, unready, Synthetic.K.AddHours(3));
        Program.Check(mapped.Items.Length == 3 && mapped.Items.All(i => i.Rank is null), "All-not-ready returns full universe");

        // A separately registered synthetic version verifies that HTTP reads do not
        // depend on the executable's single calculator registration. Copy exact
        // immutable lineage while changing only model/batch/snapshot identities.
        await CloneVersion(factory, exact.Id, "synthetic-v2");
        var other = await reader.ReadAsync(request with { ModelId = "synthetic-v2" }, default);
        Program.Check(other.Manifest.ModelId == "synthetic-v2" && other.AsOfUtc == Synthetic.T &&
            other.Items.All(i => i.Score.ModelId == "synthetic-v2"), "Stored-model isolation without calculator execution");
        Program.Check((await reader.ReadAsync(request, default)).AsOfUtc == Synthetic.T.AddHours(4), "Default model remains isolated");

        // Seal a deliberately inconsistent test-only version through normal inserts.
        // Never disable immutable triggers or change a published batch.
        await CloneVersion(factory, exact.Id, "synthetic-corrupt", corruptHash: true);
        await Expect("rankings-integrity-failure", () => reader.ReadAsync(request with { ModelId = "synthetic-corrupt" }, default));

        audit.PauseNextBatchRead = true;
        var pending = reader.ReadAsync(request, default);
        await audit.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Publish(5);
        audit.Continue.TrySetResult();
        Program.Check((await pending).AsOfUtc == Synthetic.T.AddHours(4), "Repeatable Read keeps selected snapshot during concurrent publish");
        Program.Check((await reader.ReadAsync(request, default)).AsOfUtc == Synthetic.T.AddHours(5), "Next request sees newly committed whole batch");

        var before = await HashDatabase(factory);
        for (var hour = 0; hour <= 5; hour++) await reader.ReadAsync(request with { AsOfUtc = Synthetic.T.AddHours(hour) }, default);
        await using (var blocker = factory.CreateDbContext())
        {
            await using var tx = await blocker.Database.BeginTransactionAsync();
            await blocker.Database.ExecuteSqlRawAsync("LOCK TABLE research.\"ScoringModels\" IN ACCESS EXCLUSIVE MODE");
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            try { await reader.ReadAsync(request, cancel.Token); throw new Exception("Blocked DB read ignored cancellation"); }
            catch (OperationCanceledException) { Program.Check(true, "Blocked real PostgreSQL read cancels"); }
        }
        Program.Check(before == await HashDatabase(factory), "Success, absence and cancellation preserve complete database state");
        Program.Check(audit.ReadOnlyChecks > 0 && audit.Commands.All(sql =>
            !new[] { "\"Observations\"", "\"ProviderPayloads\"", "\"InputObservations\"", "\"InputConflicts\"", "\"InputJson\"", "\"DetailJson\"", "INSERT ", "UPDATE ", "DELETE " }.Any(sql.Contains)), "Read projection excludes source facts, replay documents and writes");
        Console.WriteLine(JsonSerializer.Serialize(new { mode = "m4-database-checks", assertions = Program.Count, readOnlyTransactions = audit.ReadOnlyChecks,
            databaseHash = before, manifestHash = ScoringModel.Slice1.Hash, sourceHash = ScoringModel.Slice1.SourceHash }));
    }
    private static async Task Expect(string code, Func<Task<RankingsReadBatch>> action)
    {
        try { await action(); throw new Exception("Expected failure: " + code); }
        catch (RankingsReadException e) { Program.Check(e.Code == code, "Expected read error " + code); }
    }
    private static async Task<string> HashDatabase(Factory factory)
    {
        await using var db = factory.CreateDbContext();
        var documents = new List<string>();
        foreach (var table in new[] { "Assets", "ProviderInstrumentRefs", "Providers", "Observations", "ProviderPayloads", "Quarantine",
            "ScoringModels", "ScoringBatches", "FeatureSnapshots", "FeatureValues", "ScoreSnapshots", "CategoryScores", "InputObservations", "InputConflicts" })
        {
            // Identifiers come only from the fixed table list above, never input.
            var sql = $"SELECT row_to_json(t)::text AS \"Value\" FROM research.\"{table}\" t";
            var rows = await db.Database.SqlQueryRaw<string>(sql).ToArrayAsync();
            documents.AddRange(rows.Order(StringComparer.Ordinal));
        }
        return CanonicalJson.Hash(CanonicalJson.Write(documents));
    }
    private static async Task CloneVersion(Factory factory, string sourceId, string modelId, bool corruptHash = false)
    {
        await using var db = factory.CreateDbContext(); await using var tx = await db.Database.BeginTransactionAsync();
        var source = await db.Set<ScoringBatchRow>().AsNoTracking().SingleAsync(b => b.Id == sourceId);
        var model = ScoringModel.Slice1.Manifest with { ModelId = modelId };
        var manifest = CanonicalJson.Write(model); var batchId = CanonicalJson.Hash("m4-" + modelId);
        db.Add(new ScoringModelRow { Id = model.ModelId, ManifestJson = manifest, ManifestHash = CanonicalJson.Hash(manifest), SourceHash = ScoringModel.Slice1.SourceHash, CreatedAtUtc = Synthetic.K });
        source.Id = batchId; source.ModelId = model.ModelId; source.CreatingTransactionId = "";
        db.Add(source); await db.SaveChangesAsync();
        // Default value must be used for the creating transaction (EF omits empty default strings).
        foreach (var original in await db.Set<FeatureSnapshotRow>().AsNoTracking().Where(f => f.BatchId == sourceId).ToArrayAsync())
        {
            var originalId = original.Id; var id = CanonicalJson.Hash(originalId + model.ModelId);
            var values = await db.Set<FeatureValueRow>().AsNoTracking().Where(v => v.SnapshotId == originalId).ToArrayAsync();
            original.Id = id; original.BatchId = batchId; original.ModelId = model.ModelId;
            original.FeatureHash = CanonicalJson.Hash(CanonicalJson.Write(new FeatureSet(original.AssetId, original.AsOfUtc, model.ModelId,
                original.CorePriceReady, values.OrderBy(v => v.FeatureId).Select(v => CanonicalJson.Read<FeatureValue>(v.DetailJson)).ToArray())));
            db.Add(original);
            foreach (var value in values) { value.SnapshotId = id; value.BatchId = batchId; db.Add(value); }
            var score = await db.Set<ScoreSnapshotRow>().AsNoTracking().SingleAsync(s => s.Id == originalId);
            score.Id = id; score.SnapshotId = id; score.BatchId = batchId; score.ModelId = model.ModelId;
            score.ScoreJson = CanonicalJson.Write(CanonicalJson.Read<ScoreResult>(score.ScoreJson) with { ModelId = model.ModelId });
            score.ScoreHash = corruptHash ? new string('f', 64) : CanonicalJson.Hash(score.ScoreJson); db.Add(score);
            foreach (var c in await db.Set<CategoryScoreRow>().AsNoTracking().Where(c => c.ScoreId == originalId).ToArrayAsync())
            { c.ScoreId = id; c.BatchId = batchId; db.Add(c); }
        }
        foreach (var input in await db.Set<InputObservationRow>().AsNoTracking().Where(i => i.BatchId == sourceId).ToArrayAsync())
        { input.BatchId = batchId; db.Add(input); }
        foreach (var input in await db.Set<InputConflictRow>().AsNoTracking().Where(i => i.BatchId == sourceId).ToArrayAsync())
        { input.BatchId = batchId; db.Add(input); }
        await db.SaveChangesAsync(); await tx.CommitAsync();
    }
    private sealed class ReadAudit : DbCommandInterceptor
    {
        public ConcurrentQueue<string> Commands { get; } = new();
        public int ReadOnlyChecks; public bool PauseNextBatchRead;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.CommandText);
            if (PauseNextBatchRead && command.CommandText.Contains("\"ScoringBatches\"", StringComparison.Ordinal))
            { PauseNextBatchRead = false; Entered.TrySetResult(); await Continue.Task.WaitAsync(cancellationToken); }
            return result;
        }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            Commands.Enqueue(command.CommandText);
            if (command.CommandText == "SET TRANSACTION READ ONLY")
            {
                await using var check = command.Connection!.CreateCommand(); check.Transaction = command.Transaction;
                check.CommandText = "SHOW transaction_read_only";
                Program.Check((string)(await check.ExecuteScalarAsync(cancellationToken))! == "on", "Database enforces read-only transaction");
                ReadOnlyChecks++;
            }
            return result;
        }
    }
}
