using System.Data;
using Analysis.Application;
using Analysis.Domain.Scoring;
using Analysis.Domain.SignalsOutcomes;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Infrastructure.Persistence;

public sealed class ForwardRecordReader(IDbContextFactory<ResearchDbContext> factory) : IForwardRecordReader
{
    public async Task<ForwardInspectionReport> InspectAsync(ForwardRange range, CancellationToken cancellationToken)
    {
        if (range.AfterId is not null) throw new ForwardPreconditionException("inspection-does-not-filter-by-cursor");
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        await ScoringStore.PreconditionsAsync(db, cancellationToken);
        var now = await ForwardStore.ClockAsync(db, cancellationToken);
        var rows = await ForwardStore.RangeRowsAsync(db, range, cancellationToken);
        var records = new List<ForwardInspection>();
        foreach (var row in rows)
        {
            var original = await ForwardStore.ReadOriginalAsync(db, row, cancellationToken);
            var assessmentRows = await db.Set<ForwardOutcomeAssessmentRow>().AsNoTracking().Where(a => a.IssuanceId == row.Id).ToArrayAsync(cancellationToken);
            var history = new List<StoredOutcome>();
            foreach (var assessment in assessmentRows.OrderBy(a => a.AssetId, StringComparer.Ordinal).ThenBy(a => a.HorizonHours).ThenBy(a => a.Sequence))
                history.Add(await ForwardStore.ReadAssessmentAsync(db, original, assessment, cancellationToken));
            var latest = history.GroupBy(a => new { a.Result.Target.AssetId, a.Result.Target.HorizonHours }).Select(g => g.Last()).ToArray();
            var unassessed = ForwardMethodology.Targets(original).Where(t => !latest.Any(a => a.Result.Target == t)).ToArray();
            records.Add(new(original, latest, history.ToArray(), unassessed));
        }
        var events = await db.Set<ForwardRunEventRow>().AsNoTracking().Where(e => e.StartUtc < range.EndUtc && e.EndUtc > range.StartUtc)
            .OrderBy(e => e.AtUtc).ThenBy(e => e.Id).ToArrayAsync(cancellationToken);
        // Count only fully elapsed issuance windows wholly inside the report range.
        // A partial first/last hour or an open 15-minute window is not a missed issue.
        var slots = 0;
        var issuedHours = await db.Set<ForwardIssuanceRow>().AsNoTracking()
            .Where(r => r.AsOfUtc >= range.StartUtc && r.AsOfUtc < range.EndUtc)
            .Select(r => r.AsOfUtc).ToArrayAsync(cancellationToken);
        for (var t = ForwardMethodology.Hour(range.StartUtc); t.AddMinutes(15) <= range.EndUtc && t.AddMinutes(15) < now; t = t.AddHours(1))
            if (t >= range.StartUtc && !issuedHours.Contains(t)) slots++;
        return new(now, range, records.ToArray(), BenchmarkCalculator.Aggregate(records), slots,
            events.Select(e => (object)new { e.RunId, e.Operation, e.State, e.AtUtc, e.StartUtc, e.EndUtc,
                Detail = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(e.DetailJson) }).ToArray());
    }
}
