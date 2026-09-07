using System.Data;
using System.Security.Cryptography;
using System.Text;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Analysis.Domain.SignalsOutcomes;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Infrastructure.Persistence;

public sealed class ForwardObservationStore(IDbContextFactory<ResearchDbContext> factory, IObservationStore legacy) : IForwardCollectionStore
{
    public async Task<ForwardCollectionWork[]> PlanInputsAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        await ScoringStore.PreconditionsAsync(db, cancellationToken);
        var now = await ForwardStore.ClockAsync(db, cancellationToken);
        if (asOfUtc != ForwardMethodology.Hour(now) || asOfUtc.AddMilliseconds(1) > now) throw new ForwardPreconditionException("invalid-input-collection-hour");
        var instruments = await db.Instruments.AsNoTracking().ToArrayAsync(cancellationToken);
        return instruments.OrderBy(i => i.Id, StringComparer.Ordinal).Select(i => new ForwardCollectionWork(i,
            new(asOfUtc.AddHours(-ForwardMethodology.Manifest.InputLookbackHours), i.Kind == InstrumentKind.Spot ? asOfUtc : asOfUtc.AddMilliseconds(1)))).ToArray();
    }
    public async Task<ForwardCollectionWork[]> PlanOutcomesAsync(ReadWindow window, CancellationToken cancellationToken)
    {
        window.Validate();
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        await ScoringStore.PreconditionsAsync(db, cancellationToken);
        var now = await ForwardStore.ClockAsync(db, cancellationToken);
        if (window.EndUtc > ForwardMethodology.Hour(now) || window.EndUtc - window.StartUtc > TimeSpan.FromDays(7) ||
            window.StartUtc != ForwardMethodology.Hour(window.StartUtc) || window.EndUtc != ForwardMethodology.Hour(window.EndUtc))
            throw new ForwardPreconditionException("invalid-outcome-collection-window");
        var targets = await db.Set<ForwardOutcomeTargetRow>().AsNoTracking().Where(t => t.ReferenceBoundaryUtc.AddHours(-1) < window.EndUtc && t.MaturityUtc > window.StartUtc).ToArrayAsync(cancellationToken);
        var instruments = await db.Instruments.AsNoTracking().ToArrayAsync(cancellationToken);
        var work = new List<ForwardCollectionWork>();
        foreach (var group in targets.GroupBy(t => t.InstrumentId).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var instrument = instruments.Single(i => i.Id == group.Key);
            ForwardStore.Require(instrument.ProviderId == "binance" && instrument.Kind == InstrumentKind.Spot && instrument.QuoteUnit == "USDT");
            var intervals = group.Select(t => new ReadWindow(t.ReferenceBoundaryUtc.AddHours(-1) > window.StartUtc ? t.ReferenceBoundaryUtc.AddHours(-1) : window.StartUtc,
                t.MaturityUtc < window.EndUtc ? t.MaturityUtc : window.EndUtc)).OrderBy(w => w.StartUtc).ThenBy(w => w.EndUtc).ToArray();
            ReadWindow? current = null;
            foreach (var interval in intervals)
            {
                if (current is null) current = interval;
                else if (interval.StartUtc <= current.EndUtc) current = current with { EndUtc = interval.EndUtc > current.EndUtc ? interval.EndUtc : current.EndUtc };
                else { work.Add(new(instrument, current)); current = interval; }
            }
            if (current is not null) work.Add(new(instrument, current));
        }
        return work.ToArray();
    }

    public async Task<ForwardStoreResult> SaveAsync(ForwardCollectionWork work, IReadOnlyList<ObservationPage> pages, CancellationToken cancellationToken)
    {
        var instrument = work.Instrument; var window = work.Window; window.Validate();
        if (window.EndUtc - window.StartUtc > TimeSpan.FromDays(7)) throw new ProviderReadException("window-budget-exhausted");
        foreach (var page in pages)
        {
            if (page.Payload.Bytes.Length > 4 * 1024 * 1024 || page.Payload.RequestPath.Length > 2048 ||
                !page.Payload.RequestPath.StartsWith('/') || page.Payload.MappingVersion.Length is 0 or > 64) throw new ProviderReadException("invalid-provenance");
            foreach (var observation in page.Observations)
            {
                try { observation.Validate(instrument); }
                catch (Exception error) when (error is ArgumentException or FormatException) { throw new ProviderReadException("invalid-observation"); }
                if (observation.EventTimeUtc < window.StartUtc || observation.EventTimeUtc >= window.EndUtc ||
                    observation.Kind == ObservationKind.Candle && observation.EventTimeUtc.AddHours(1) > window.EndUtc) throw new ProviderReadException("observation-outside-window");
            }
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({instrument.Id}, 0))", cancellationToken);
        await ScoringStore.PreconditionsAsync(db, cancellationToken);
        var now = await ForwardStore.ClockAsync(db, cancellationToken);
        if (window.EndUtc > now) throw new ProviderReadException("future-window");
        var existing = await db.Observations.AsNoTracking().Where(o => o.InstrumentId == instrument.Id && o.EventTimeUtc >= window.StartUtc && o.EventTimeUtc < window.EndUtc).ToArrayAsync(cancellationToken);
        var facts = existing.ToDictionary(o => new ObservationKey(o.InstrumentId, o.Kind, o.EventTimeUtc, o.PeriodSeconds));
        var payloadIds = existing.Select(o => o.PayloadId).Distinct().ToArray();
        var payloads = (await db.Payloads.AsNoTracking().Where(p => payloadIds.Contains(p.Id)).ToArrayAsync(cancellationToken)).ToDictionary(p => p.Id);
        var quarantines = new HashSet<string>(); var candidateIds = new HashSet<string>();
        var inserted = 0; var duplicates = 0; var conflicts = 0;
        foreach (var page in pages)
        {
            var sha = Convert.ToHexStringLower(SHA256.HashData(page.Payload.Bytes));
            var payloadId = Hash($"{instrument.Id}|{page.Payload.MappingVersion}|{page.Payload.RequestPath}|{window.StartUtc:O}|{window.EndUtc:O}|{sha}");
            if (!payloads.TryGetValue(payloadId, out var payload))
            {
                payload = await db.Payloads.AsNoTracking().SingleOrDefaultAsync(p => p.Id == payloadId, cancellationToken);
                if (payload is null)
                {
                    payload = new() { Id = payloadId, InstrumentId = instrument.Id, Sha256 = sha, Bytes = page.Payload.Bytes,
                        RequestPath = page.Payload.RequestPath, MappingVersion = page.Payload.MappingVersion,
                        WindowStartUtc = window.StartUtc, WindowEndUtc = window.EndUtc, IngestedAtUtc = now };
                    db.Payloads.Add(payload);
                }
                payloads.Add(payloadId, payload);
            }
            foreach (var o in page.Observations)
            {
                var key = ObservationKey.Of(o);
                if (!facts.TryGetValue(key, out var previous))
                {
                    var row = ObservationRow.From(o, payloadId, now); db.Observations.Add(row); facts.Add(key, row); inserted++; continue;
                }
                if (previous.ToObservation() == o) { duplicates++; continue; }
                conflicts++;
                var end = o.EventTimeUtc.AddMilliseconds(o.Kind == ObservationKind.Candle || o.Kind == ObservationKind.OpenInterestBothSides ? 3_600_000 : 1);
                var quarantineId = Hash($"{instrument.Id}|{o.EventTimeUtc:O}|{end:O}|conflicting-observation");
                if (quarantines.Add(quarantineId) && !await db.Quarantine.AnyAsync(q => q.Id == quarantineId, cancellationToken))
                    db.Quarantine.Add(new() { Id = quarantineId, InstrumentId = instrument.Id, Code = "conflicting-observation",
                        WindowStartUtc = o.EventTimeUtc, WindowEndUtc = end, IngestedAtUtc = now });
                var candidateJson = CanonicalJson.Write(o);
                var id = CanonicalJson.Hash(CanonicalJson.Write(new { key, candidateJson, payloadId }));
                if (candidateIds.Add(id) && !await db.Set<ForwardObservationConflictRow>().AnyAsync(c => c.Id == id, cancellationToken))
                {
                    var originalPayload = payloads[previous.PayloadId];
                    var original = new ObservationFact(previous.ToObservation(), previous.PayloadId, originalPayload.MappingVersion, originalPayload.Sha256, previous.IngestedAtUtc);
                    db.Add(new ForwardObservationConflictRow { Id = id, InstrumentId = instrument.Id, Kind = o.Kind, EventTimeUtc = o.EventTimeUtc,
                        PeriodSeconds = o.PeriodSeconds, QuarantineId = quarantineId, CandidatePayloadId = payloadId,
                        OriginalFactJson = CanonicalJson.Write(original), CandidateJson = candidateJson, DetectedAtUtc = now });
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new(inserted, duplicates, conflicts);
    }
    public async Task QuarantineAsync(ForwardCollectionWork work, string code, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await legacy.QuarantineAsync(work.Instrument, work.Window, code, await ForwardStore.ClockAsync(db, cancellationToken), cancellationToken);
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
