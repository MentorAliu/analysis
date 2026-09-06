using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Analysis.CatalogChecks;

internal static class DatabaseChecks
{
    public static string? Snapshot { get; private set; }
    private static Factory CreateFactory()
    {
        if (Environment.GetEnvironmentVariable("M2_ISOLATED_TEST") != "true")
            throw new InvalidOperationException("Database checks require a fresh disposable project via scripts/verify-m2.mjs.");
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = "postgres", Database = "analysis_m2_checks", Username = "analysis",
            Password = Environment.GetEnvironmentVariable("M2_DB_PASSWORD") ?? throw new InvalidOperationException("Missing isolated test password"),
            Timeout = 3, CommandTimeout = 10, Timezone = "UTC", IncludeErrorDetail = false
        };
        return new(new DbContextOptionsBuilder<ResearchDbContext>().UseNpgsql(connection.ConnectionString,
            options => options.SetPostgresVersion(18, 0)).Options);
    }

    public static async Task RunAsync(FixtureServer server, IObservationAdapter[] adapters, ReadWindow window)
    {
        var factory = CreateFactory();
        await using (var db = factory.CreateDbContext())
        {
            Check.That(!(await db.Database.GetAppliedMigrationsAsync()).Any(), "Refuse destructive tests on an already migrated database");
            await db.Database.MigrateAsync();
            Check.That(await db.Assets.CountAsync() == 3 && await db.Instruments.CountAsync() == 8, "Catalog migration seed");
            Check.That(await db.Providers.AllAsync(p => p.ApprovalStatus == "Unresolved"), "No implied live approval");
            Check.That(!db.Database.HasPendingModelChanges(), "EF model/migration drift");
            await db.GetService<IMigrator>().MigrateAsync("0");
            Check.That(!(await db.Database.GetAppliedMigrationsAsync()).Any(), "Disposable down migration");
            await db.Database.MigrateAsync();
        }
        Check.Pass("EF migration on empty PostgreSQL, catalog seeds, no model drift, disposable rollback/reapply");

        var store = new ObservationStore(factory);
        var ingestion = new ObservationIngestion(store, new FrozenClock(window.EndUtc.AddHours(1)));
        var first = await ingestion.RunAsync(CatalogSeed.Instruments, adapters, window, CancellationToken.None);
        Check.That(first.All(r => r.Status == "stored") && first.Sum(r => r.Inserted) == 14, "Eight refs produce 14 test observations");
        var before = await SnapshotAsync(factory);
        var second = await ingestion.RunAsync(CatalogSeed.Instruments, adapters, window, CancellationToken.None);
        Check.That(second.Sum(r => r.Inserted) == 0 && second.Sum(r => r.Duplicates) == 14, "Idempotent rerun");
        Check.That(before == await SnapshotAsync(factory), "Identical rerun changes no stored facts, payloads or timestamps");
        await using (var db = factory.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            Check.That(before == await SnapshotAsync(factory), "Reapplying migrations preserves representative populated data");
            var fact = await db.Observations.FirstAsync(o => o.Kind == ObservationKind.Candle);
            Check.That(fact.Close == 0.01577100m && fact.QuoteUnit == "USDT" && fact.EventTimeUtc.Offset == TimeSpan.Zero, "Exact numeric/time/unit roundtrip");
            var payload = await db.Payloads.SingleAsync(p => p.Id == fact.PayloadId);
            Check.That(payload.Sha256 == Convert.ToHexStringLower(SHA256.HashData(payload.Bytes)) && payload.MappingVersion == "binance-spot-v1", "Raw byte/hash/version lineage");
        }
        Check.Pass("Transactional ingestion, numeric/UTC roundtrip, byte/hash lineage and unchanged idempotent rerun/migration");

        // Fresh windows exercise actual concurrent INSERTs, not merely read-only duplicate detection.
        var spot = CatalogSeed.Instruments.First(i => i.Kind == InstrumentKind.Spot);
        var fresh = new ReadWindow(window.EndUtc, window.EndUtc.AddDays(1));
        var original = BinanceCandle(spot, window.StartUtc);
        ObservationPage[] concurrentPage = [new(new("/offline/concurrent", "fixture-concurrency-v1", "[]"u8.ToArray()), [original with { EventTimeUtc = fresh.StartUtc }])];
        var saves = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => store.SaveAsync(spot, fresh,
            concurrentPage, fresh.EndUtc, CancellationToken.None)));
        Check.That(saves.Sum(r => r.Inserted) == 1 && saves.Sum(r => r.Duplicates) == 3, "Concurrent insert deduplication");
        Check.Pass("Four concurrent instrument writes create one logical observation");

        server.Conflict = true;
        var conflict = await ingestion.RunAsync([spot], adapters, window, CancellationToken.None);
        Check.That(conflict.Single().ErrorCode == "conflicting-observation", "Conflicts are quarantined");
        server.Conflict = false;
        await using (var db = factory.CreateDbContext())
        {
            Check.That(await db.Observations.CountAsync() == 15, "Conflict cannot add or overwrite facts");
            Check.That(await db.Observations.Where(o => o.Kind == ObservationKind.Candle).AllAsync(o => o.Close == 0.01577100m), "Original candle survives conflict");
            Check.That(await db.Quarantine.CountAsync() == 1, "Visible quarantine");
        }
        server.FailEther = true;
        var partial = await ingestion.RunAsync(CatalogSeed.Instruments.Where(i => i.Kind == InstrumentKind.LinearPerpetual).ToArray(), adapters, window, CancellationToken.None);
        server.FailEther = false;
        Check.That(partial.Count(r => r.Status == "quarantined") == 1 && partial.Count(r => r.Status == "stored") == 2, "One failed asset does not abort unrelated assets");
        Check.Pass("Conflicting history preserved, quarantine recorded and partial asset failure isolated");

        await using (var db = factory.CreateDbContext())
        {
            var unrelated = new ObservationRow { InstrumentId = spot.Id, Kind = ObservationKind.ChainTvl,
                EventTimeUtc = fresh.StartUtc, PeriodSeconds = 0, Unit = "USD", Value = 1,
                PayloadId = "nonexistent", IngestedAtUtc = fresh.EndUtc };
            db.Observations.Add(unrelated);
            await Check.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(), "Missing lineage FK rejected");
        }
        await using (var db = factory.CreateDbContext())
        {
            var wrongPayload = await db.Payloads.FirstAsync(p => p.InstrumentId != spot.Id);
            db.Observations.Add(ObservationRow.From(original with { EventTimeUtc = fresh.StartUtc.AddHours(1) }, wrongPayload.Id, fresh.EndUtc));
            await Check.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(), "Cross-instrument lineage rejected");
        }
        await using (var db = factory.CreateDbContext())
        {
            var payload = await db.Payloads.FirstAsync(p => p.InstrumentId == spot.Id);
            var invalid = ObservationRow.From(original with { EventTimeUtc = fresh.StartUtc.AddHours(1) }, payload.Id, fresh.EndUtc);
            invalid.Open = null;
            db.Observations.Add(invalid);
            await Check.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(), "NULL candle field rejected by database");
        }
        var unchanged = await SnapshotAsync(factory);
        await using (var blockingDb = factory.CreateDbContext())
        {
            await using var blockingTransaction = await blockingDb.Database.BeginTransactionAsync();
            await blockingDb.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({spot.Id}, 0))");
            using var cancelled = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));
            await Check.ThrowsAsync<OperationCanceledException>(() => store.SaveAsync(spot, fresh, concurrentPage, fresh.EndUtc, cancelled.Token), "Cancelled database I/O");
        }
        Check.That(unchanged == await SnapshotAsync(factory), "Cancelled save changes no data");
        Check.Pass("Database lineage constraint and cancellation preserve persisted state");
        Snapshot = await SnapshotAsync(factory);
    }

    public static async Task<string> PersistenceSnapshotAsync() => await SnapshotAsync(CreateFactory());

    private static async Task<string> SnapshotAsync(Factory factory)
    {
        await using var db = factory.CreateDbContext();
        var assets = await db.Assets.OrderBy(x => x.Id).ToArrayAsync();
        var instruments = await db.Instruments.OrderBy(x => x.Id).ToArrayAsync();
        var observations = await db.Observations.OrderBy(x => x.InstrumentId).ThenBy(x => x.Kind).ThenBy(x => x.EventTimeUtc).ToArrayAsync();
        var payloads = await db.Payloads.OrderBy(x => x.Id).ToArrayAsync();
        var quarantine = await db.Quarantine.OrderBy(x => x.Id).ToArrayAsync();
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { assets, instruments, observations, payloads, quarantine }))));
    }

    private static Observation BinanceCandle(InstrumentRef spot, DateTimeOffset time) =>
        new(spot.Id, ObservationKind.Candle, time, 3600, spot.BaseUnit, spot.QuoteUnit,
            0.01634790m, 0.8m, 0.015758m, 0.01577100m, 148976.11427815m, 2434.19055334m);

    private sealed class Factory(DbContextOptions<ResearchDbContext> options) : IDbContextFactory<ResearchDbContext>
    {
        public ResearchDbContext CreateDbContext() => new(options);
    }
    private sealed class FrozenClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
