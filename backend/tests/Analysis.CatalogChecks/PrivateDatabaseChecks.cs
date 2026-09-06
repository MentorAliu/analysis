using System.Security.Cryptography;
using System.Text.Json;
using Analysis.Domain;
using Analysis.Infrastructure.Adapters;
using Analysis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Analysis.CatalogChecks;

internal static class PrivateDatabaseChecks
{
    // Read-only acceptance inspection. Never print raw provider data or financial values.
    public static async Task<object> ReadAsync(IDbContextFactory<ResearchDbContext> factory, ReadWindow window)
    {
        await using var db = await factory.CreateDbContextAsync();
        var instruments = await db.Instruments.AsNoTracking().OrderBy(i => i.Id).ToArrayAsync();
        Check.That(instruments.SequenceEqual(CatalogSeed.Instruments.OrderBy(i => i.Id)), "Private catalog matches reviewed scope");
        var rows = await db.Observations.AsNoTracking().OrderBy(o => o.InstrumentId).ThenBy(o => o.Kind).ThenBy(o => o.EventTimeUtc).ToArrayAsync();
        var payloads = await db.Payloads.AsNoTracking().OrderBy(p => p.Id).ToArrayAsync();
        var quarantines = await db.Quarantine.AsNoTracking().OrderBy(q => q.Id).ToArrayAsync();
        var replayed = new Dictionary<string, IReadOnlyList<Observation>>();
        foreach (var payload in payloads)
        {
            Check.That(payload.Sha256 == Hash(payload.Bytes), "Exact raw-byte SHA256 provenance");
            Check.That(payload.WindowStartUtc == window.StartUtc && payload.WindowEndUtc == window.EndUtc &&
                payload.IngestedAtUtc >= window.EndUtc && payload.IngestedAtUtc.Offset == TimeSpan.Zero, "Payload window/ingestion UTC lineage");
            var instrument = instruments.Single(i => i.Id == payload.InstrumentId);
            var observations = payload.RequestPath switch
            {
                var path when path.StartsWith("/api/v3/klines?", StringComparison.Ordinal) => BinanceMarketAdapter.ParseCandles(payload.Bytes, instrument, window).Observations,
                var path when path.StartsWith("/v5/market/funding/history?", StringComparison.Ordinal) => BybitDerivativesAdapter.Parse(payload.Bytes, instrument, window, ObservationKind.FundingRate).Observations,
                var path when path.StartsWith("/v5/market/open-interest?", StringComparison.Ordinal) => BybitDerivativesAdapter.Parse(payload.Bytes, instrument, window, ObservationKind.OpenInterestBothSides).Observations,
                var path when path.StartsWith("/v2/historicalChainTvl/", StringComparison.Ordinal) => DefiLlamaFundamentalsAdapter.Parse(payload.Bytes, instrument, window),
                _ => Array.Empty<Observation>()
            };
            var version = instrument.ProviderId switch { "binance" => "binance-spot-v1", "bybit" => "bybit-linear-v1", _ => "defillama-chain-tvl-v1" };
            Check.That(payload.MappingVersion == version, "Known immutable mapping version");
            replayed.Add(payload.Id, observations);
        }
        foreach (var row in rows)
        {
            var observation = row.ToObservation();
            observation.Validate(instruments.Single(i => i.Id == row.InstrumentId));
            Check.That(observation.EventTimeUtc >= window.StartUtc && observation.EventTimeUtc < window.EndUtc &&
                row.IngestedAtUtc >= window.EndUtc && row.IngestedAtUtc.Offset == TimeSpan.Zero, "Stored UTC and window bounds");
            Check.That(replayed[row.PayloadId].Contains(observation), "Persisted decimals and units exactly replay from raw payload");
        }
        var coverage = instruments.SelectMany(instrument =>
        {
            ObservationKind[] kinds = instrument.Kind switch
            {
                InstrumentKind.Spot => [ObservationKind.Candle],
                InstrumentKind.LinearPerpetual => [ObservationKind.FundingRate, ObservationKind.OpenInterestBothSides],
                _ => [ObservationKind.ChainTvl]
            };
            return kinds.Select(kind =>
            {
                var matching = rows.Where(r => r.InstrumentId == instrument.Id && r.Kind == kind).ToArray();
                var hourly = kind is ObservationKind.Candle or ObservationKind.OpenInterestBothSides;
                var expected = (int)(window.EndUtc - window.StartUtc).TotalHours;
                var timestamps = matching.Select(r => r.EventTimeUtc).ToHashSet();
                var missingHours = hourly ? Enumerable.Range(0, expected).Count(hour => !timestamps.Contains(window.StartUtc.AddHours(hour))) : 0;
                return new { instrument = instrument.Id, kind = kind.ToString(), count = matching.Length, missingHours,
                    firstUtc = matching.FirstOrDefault()?.EventTimeUtc, lastUtc = matching.LastOrDefault()?.EventTimeUtc };
            });
        }).ToArray();
        return new
        {
            mode = "private-snapshot", coverage, observationCount = rows.Length, payloadCount = payloads.Length,
            observationSnapshot = Hash(JsonSerializer.SerializeToUtf8Bytes(rows)),
            databaseSnapshot = Hash(JsonSerializer.SerializeToUtf8Bytes(new { instruments, rows, payloads, quarantines })),
            quarantine = quarantines.Select(q => new { q.InstrumentId, q.Code }).ToArray(),
            completeCoverage = coverage.All(c => c.count > 0 && c.missingHours == 0),
            replay = "passed: exact decimals, units, UTC, raw-byte hashes and observation lineage"
        };
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
