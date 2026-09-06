using System.Security.Cryptography;
using System.Text;
using Analysis.Application;
using Analysis.Domain;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Infrastructure.Persistence;

public sealed class ObservationStore(IDbContextFactory<ResearchDbContext> factory) : IObservationStore
{
    public async Task<StoreResult> SaveAsync(InstrumentRef instrument, ReadWindow window,
        IReadOnlyList<ObservationPage> pages, DateTimeOffset ingestedAtUtc, CancellationToken cancellationToken)
    {
        window.Validate(); Utc.Require(ingestedAtUtc);
        if (ingestedAtUtc < window.EndUtc) throw new ArgumentException("Ingestion cannot precede the window end.");
        foreach (var page in pages)
        {
            if (page.Payload.Bytes.Length > 4 * 1024 * 1024 || page.Payload.RequestPath.Length > 2048 ||
                !page.Payload.RequestPath.StartsWith('/') || page.Payload.MappingVersion.Length is 0 or > 64)
                throw new ProviderReadException("invalid-provenance");
            foreach (var observation in page.Observations)
            {
                try { observation.Validate(instrument); }
                catch (Exception error) when (error is ArgumentException or FormatException) { throw new ProviderReadException("invalid-observation"); }
                if (observation.EventTimeUtc < window.StartUtc || observation.EventTimeUtc >= window.EndUtc ||
                    (observation.Kind == ObservationKind.Candle && observation.EventTimeUtc.AddSeconds(3600) > window.EndUtc))
                    throw new ProviderReadException("observation-outside-window");
            }
        }
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        // Serialize each instrument's ingestion across processes; database keys remain the final guard.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({instrument.Id}, 0))", cancellationToken);
        var known = await db.Instruments.SingleAsync(i => i.Id == instrument.Id, cancellationToken);
        if (known != instrument) throw new ProviderReadException("catalog-mismatch");
        var existing = await db.Observations.Where(o => o.InstrumentId == instrument.Id &&
            o.EventTimeUtc >= window.StartUtc && o.EventTimeUtc < window.EndUtc).ToListAsync(cancellationToken);
        var facts = existing.ToDictionary(o => (o.Kind, o.EventTimeUtc, o.PeriodSeconds), o => o.ToObservation());
        var payloadsAdded = new HashSet<string>();
        var inserted = 0; var duplicates = 0;
        foreach (var page in pages)
        {
            var sha = Convert.ToHexStringLower(SHA256.HashData(page.Payload.Bytes));
            var payloadId = Hash($"{instrument.Id}|{page.Payload.MappingVersion}|{page.Payload.RequestPath}|{window.StartUtc:O}|{window.EndUtc:O}|{sha}");
            if (payloadsAdded.Add(payloadId) && !await db.Payloads.AnyAsync(p => p.Id == payloadId, cancellationToken))
                db.Payloads.Add(new PayloadRow
                {
                    Id = payloadId, InstrumentId = instrument.Id, Sha256 = sha, Bytes = page.Payload.Bytes,
                    RequestPath = page.Payload.RequestPath, MappingVersion = page.Payload.MappingVersion,
                    WindowStartUtc = window.StartUtc, WindowEndUtc = window.EndUtc, IngestedAtUtc = ingestedAtUtc
                });
            foreach (var o in page.Observations)
            {
                var key = (o.Kind, o.EventTimeUtc, o.PeriodSeconds);
                if (facts.TryGetValue(key, out var previous))
                {
                    if (previous != o) throw new ProviderReadException("conflicting-observation");
                    duplicates++;
                }
                else
                {
                    facts.Add(key, o);
                    db.Observations.Add(ObservationRow.From(o, payloadId, ingestedAtUtc));
                    inserted++;
                }
            }
        }
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(inserted, duplicates);
    }

    public async Task QuarantineAsync(InstrumentRef instrument, ReadWindow window, string code,
        DateTimeOffset ingestedAtUtc, CancellationToken cancellationToken)
    {
        window.Validate(); Utc.Require(ingestedAtUtc);
        if (code.Length is 0 or > 80 || code.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-'))
            throw new ArgumentException("Quarantine codes must be safe identifiers.");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({instrument.Id}, 0))", cancellationToken);
        var id = Hash($"{instrument.Id}|{window.StartUtc:O}|{window.EndUtc:O}|{code}");
        if (!await db.Quarantine.AnyAsync(q => q.Id == id, cancellationToken))
        {
            db.Quarantine.Add(new() { Id = id, InstrumentId = instrument.Id, Code = code,
                WindowStartUtc = window.StartUtc, WindowEndUtc = window.EndUtc, IngestedAtUtc = ingestedAtUtc });
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
