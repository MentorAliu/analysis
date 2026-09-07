using System.Data;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Analysis.Domain.SignalsOutcomes;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Infrastructure.Persistence;

public sealed class ForwardStore(IDbContextFactory<ResearchDbContext> factory) : IForwardRecordingStore, IForwardOutcomeStore
{
    internal static Task<DateTimeOffset> ClockAsync(ResearchDbContext db, CancellationToken ct) =>
        db.Database.SqlQuery<DateTimeOffset>($"SELECT research.\"ForwardClock\"() AS \"Value\"").SingleAsync(ct);
    internal static void Require(bool condition)
    { if (!condition) throw new ForwardPreconditionException("forward-integrity-failure"); }

    public async Task<ForwardOriginal?> FindAsync(string modelId, DateTimeOffset asOfUtc, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        await ScoringStore.PreconditionsAsync(db, cancellationToken);
        var row = await db.Set<ForwardIssuanceRow>().AsNoTracking().SingleOrDefaultAsync(i => i.ModelId == modelId && i.AsOfUtc == asOfUtc, cancellationToken);
        return row is null ? null : await ReadOriginalAsync(db, row, cancellationToken);
    }

    public async Task<ScoringInput> CaptureAsync(DateTimeOffset asOfUtc, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        var k = await ClockAsync(db, cancellationToken);
        ForwardMethodology.RequireIssuanceClock(asOfUtc, k, k, k);
        return await ScoringStore.CaptureInTransactionAsync(db, new(asOfUtc, k, "slice1-v1"), ScoringModel.Slice1, cancellationToken);
    }

    public async Task<ForwardIssueResult> PublishAsync(ScoringBundle bundle, CancellationToken cancellationToken)
    {
        var model = ScoringModel.Slice1; var t = bundle.Input.AsOfUtc;
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET LOCAL transaction_timeout = '30s'", cancellationToken);
        await ScoringStore.PreconditionsAsync(db, cancellationToken);
        // Same lock order as M3; held until both scoring and issuance have committed.
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"m3-model:" + model.Manifest.ModelId}, 0))", cancellationToken);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({ScoringStore.LockKey(model.Manifest.ModelId, t)}, 0))", cancellationToken);
        var existing = await db.Set<ForwardIssuanceRow>().AsNoTracking().SingleOrDefaultAsync(i => i.ModelId == model.Manifest.ModelId && i.AsOfUtc == t, cancellationToken);
        if (existing is not null) return new(await ReadOriginalAsync(db, existing, cancellationToken), true);
        if (await db.Set<ScoringBatchRow>().AnyAsync(b => b.ModelId == model.Manifest.ModelId && b.AsOfUtc == t, cancellationToken))
            throw new ForwardPreconditionException("standalone-reconstruction-not-issuable");
        var created = await ClockAsync(db, cancellationToken);
        ForwardMethodology.RequireIssuanceClock(t, bundle.Input.KnowledgeCutoffUtc, created, created);
        var stored = await ScoringStore.PublishInTransactionAsync(db, bundle, model, created, cancellationToken);
        var methodology = await db.Set<ForwardMethodologyRow>().SingleOrDefaultAsync(m => m.Id == ForwardMethodology.Manifest.Id, cancellationToken);
        if (methodology is null) db.Add(new ForwardMethodologyRow { Id = ForwardMethodology.Manifest.Id,
            ManifestJson = ForwardMethodology.Json, ManifestHash = ForwardMethodology.Hash, SourceHash = ForwardMethodology.SourceHash, CreatedAtUtc = created });
        else Require(methodology.ManifestJson == ForwardMethodology.Json && methodology.ManifestHash == ForwardMethodology.Hash && methodology.SourceHash == ForwardMethodology.SourceHash);
        var assets = await db.Assets.AsNoTracking().ToArrayAsync(cancellationToken);
        var issued = await ClockAsync(db, cancellationToken);
        var original = ForwardRecordingJobs.Original(stored, assets, created, issued);
        var json = CanonicalJson.Write(original);
        db.Add(new ForwardIssuanceRow { Id = original.Id, BatchId = stored.Id, ModelId = stored.ModelId, MethodologyId = original.MethodologyId,
            AsOfUtc = t, KnowledgeCutoffUtc = bundle.Input.KnowledgeCutoffUtc, CreatedAtUtc = created, IssuedAtUtc = issued,
            ReferenceBoundaryUtc = original.ReferenceBoundaryUtc, OriginalJson = json, OriginalHash = CanonicalJson.Hash(json) });
        await db.SaveChangesAsync(cancellationToken);
        foreach (var item in original.Items) db.Add(new ForwardIssuanceItemRow { IssuanceId = original.Id, AssetId = item.Asset.Id,
            BatchId = stored.Id, ScoreSnapshotId = item.ScoreSnapshotId, Rank = item.Rank, ItemJson = CanonicalJson.Write(item) });
        foreach (var target in ForwardMethodology.Targets(original)) db.Add(new ForwardOutcomeTargetRow { IssuanceId = target.IssuanceId,
            AssetId = target.AssetId, InstrumentId = target.InstrumentId, HorizonHours = target.HorizonHours,
            ReferenceBoundaryUtc = target.ReferenceBoundaryUtc, MaturityUtc = target.MaturityUtc });
        await db.SaveChangesAsync(cancellationToken);
        ForwardMethodology.RequireIssuanceClock(t, bundle.Input.KnowledgeCutoffUtc, issued, await ClockAsync(db, cancellationToken));
        await tx.CommitAsync(cancellationToken);
        return new(original, false);
    }

    internal static async Task<ForwardOriginal> ReadOriginalAsync(ResearchDbContext db, ForwardIssuanceRow row, CancellationToken ct)
    {
        Require(CanonicalJson.Hash(row.OriginalJson) == row.OriginalHash);
        var original = CanonicalJson.Read<ForwardOriginal>(row.OriginalJson);
        var methodology = await db.Set<ForwardMethodologyRow>().AsNoTracking().SingleAsync(m => m.Id == row.MethodologyId, ct);
        Require(methodology.ManifestJson == ForwardMethodology.Json && methodology.ManifestHash == original.MethodologyHash &&
            methodology.ManifestHash == ForwardMethodology.Hash && methodology.SourceHash == original.MethodologySourceHash &&
            methodology.SourceHash == ForwardMethodology.SourceHash);
        var batch = await db.Set<ScoringBatchRow>().AsNoTracking().SingleAsync(b => b.Id == row.BatchId, ct);
        var stored = await ScoringStore.MaterializeAsync(db, batch, true, ct);
        ScoringJobs.Verify(stored, ScoringModel.Slice1);
        var assets = await db.Assets.AsNoTracking().ToArrayAsync(ct);
        var expected = ForwardRecordingJobs.Original(stored, assets, row.CreatedAtUtc, row.IssuedAtUtc);
        Require(row.OriginalJson == CanonicalJson.Write(expected) && original.Id == row.Id && original.AsOfUtc == row.AsOfUtc &&
            original.KnowledgeCutoffUtc == row.KnowledgeCutoffUtc && original.ReferenceBoundaryUtc == row.ReferenceBoundaryUtc);
        var items = await db.Set<ForwardIssuanceItemRow>().AsNoTracking().Where(i => i.IssuanceId == row.Id).ToArrayAsync(ct);
        Require(items.Length == 3);
        foreach (var item in original.Items)
        {
            var value = items.Single(i => i.AssetId == item.Asset.Id);
            Require(value.ItemJson == CanonicalJson.Write(item) && value.Rank == item.Rank && value.BatchId == row.BatchId && value.ScoreSnapshotId == item.ScoreSnapshotId);
        }
        var targets = await db.Set<ForwardOutcomeTargetRow>().AsNoTracking().Where(i => i.IssuanceId == row.Id).ToArrayAsync(ct);
        Require(targets.Length == 12);
        foreach (var target in ForwardMethodology.Targets(original)) Require(targets.Any(r => Target(r) == target));
        return original;
    }
    internal static OutcomeTarget Target(ForwardOutcomeTargetRow row) => new(row.IssuanceId, row.AssetId, row.InstrumentId,
        row.HorizonHours, row.ReferenceBoundaryUtc, row.MaturityUtc);

    internal static void ValidateRange(ForwardRange range)
    {
        Utc.Require(range.StartUtc); Utc.Require(range.EndUtc);
        if (range.EndUtc <= range.StartUtc || range.EndUtc - range.StartUtc > TimeSpan.FromDays(7)) throw new ForwardPreconditionException("invalid-forward-range");
    }
    internal static async Task<ForwardIssuanceRow[]> RangeRowsAsync(ResearchDbContext db, ForwardRange range, CancellationToken ct)
    {
        ValidateRange(range);
        // At most 168 hourly issuances in a seven-day interval; ordinal sorting is independent of DB collation.
        var rows = await db.Set<ForwardIssuanceRow>().AsNoTracking().Where(i => i.IssuedAtUtc >= range.StartUtc && i.IssuedAtUtc < range.EndUtc).ToArrayAsync(ct);
        return rows.OrderBy(i => i.IssuedAtUtc).ThenBy(i => i.Id, StringComparer.Ordinal).ToArray();
    }
    public async Task<ForwardPage> ReadPageAsync(ForwardRange range, CancellationToken cancellationToken)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
        await ScoringStore.PreconditionsAsync(db, cancellationToken);
        var rows = await RangeRowsAsync(db, range, cancellationToken);
        var after = range.AfterId is null ? -1 : Array.FindIndex(rows, i => i.Id == range.AfterId);
        if (range.AfterId is not null && after < 0) throw new ForwardPreconditionException("invalid-forward-cursor");
        var page = rows.Skip(after + 1).Take(25).ToArray(); var items = new List<ForwardOriginal>();
        foreach (var row in page.Take(24)) items.Add(await ReadOriginalAsync(db, row, cancellationToken));
        return new(items.ToArray(), page.Length > 24 ? page[23].Id : null);
    }

    public async Task<int> MeasureAsync(string issuanceId, CancellationToken cancellationToken)
    {
        ForwardOriginal original; OutcomeCapture capture;
        await using (var db = await factory.CreateDbContextAsync(cancellationToken))
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
            await db.Database.ExecuteSqlRawAsync("SET TRANSACTION READ ONLY", cancellationToken);
            var cutoff = await ClockAsync(db, cancellationToken);
            await ScoringStore.PreconditionsAsync(db, cancellationToken);
            var row = await db.Set<ForwardIssuanceRow>().AsNoTracking().SingleAsync(i => i.Id == issuanceId, cancellationToken);
            original = await ReadOriginalAsync(db, row, cancellationToken);
            Require(cutoff >= original.IssuedAtUtc);
            var start = original.ReferenceBoundaryUtc.AddHours(-1); var end = original.ReferenceBoundaryUtc.AddHours(168);
            var ids = original.Instruments.Where(i => i.Kind == InstrumentKind.Spot).Select(i => i.Id).ToArray();
            var facts = await (from o in db.Observations.AsNoTracking() join p in db.Payloads.AsNoTracking() on o.PayloadId equals p.Id
                where ids.Contains(o.InstrumentId) && o.Kind == ObservationKind.Candle && o.EventTimeUtc >= start && o.EventTimeUtc < end && o.IngestedAtUtc <= cutoff
                select new { O = o, p.MappingVersion, p.Sha256 }).ToArrayAsync(cancellationToken);
            var conflicts = await db.Quarantine.AsNoTracking().Where(q => ids.Contains(q.InstrumentId) && q.Code == "conflicting-observation" &&
                q.WindowStartUtc < end && q.WindowEndUtc > start && q.IngestedAtUtc <= cutoff).ToArrayAsync(cancellationToken);
            var revisions = await (from r in db.Set<ForwardObservationConflictRow>().AsNoTracking()
                join p in db.Payloads.AsNoTracking() on r.CandidatePayloadId equals p.Id
                where ids.Contains(r.InstrumentId) && r.Kind == ObservationKind.Candle && r.EventTimeUtc >= start && r.EventTimeUtc < end && r.DetectedAtUtc <= cutoff
                select new { R = r, P = p }).ToArrayAsync(cancellationToken);
            capture = new(cutoff, facts.Select(f => new ObservationFact(f.O.ToObservation(), f.O.PayloadId, f.MappingVersion, f.Sha256, f.O.IngestedAtUtc)).ToArray(),
                conflicts.Select(q => new ConflictFact(q.Id, q.InstrumentId, q.WindowStartUtc, q.WindowEndUtc, q.IngestedAtUtc, q.Code)).ToArray(),
                revisions.Select(r => Revision(r.R, r.P)).ToArray());
        }
        var results = ForwardMethodology.Targets(original).Select(t => OutcomeCalculator.Calculate(t, original.Instruments.Single(i => i.Id == t.InstrumentId), capture)).ToArray();
        await using var write = await factory.CreateDbContextAsync(cancellationToken);
        await using var publication = await write.Database.BeginTransactionAsync(cancellationToken);
        await write.Database.ExecuteSqlRawAsync("SET LOCAL transaction_timeout = '30s'", cancellationToken);
        await write.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtextextended({"1a-outcome:" + issuanceId}, 0))", cancellationToken);
        var previous = await write.Set<ForwardOutcomeAssessmentRow>().AsNoTracking().Where(a => a.IssuanceId == issuanceId).ToArrayAsync(cancellationToken);
        var assessed = await ClockAsync(write, cancellationToken); Require(assessed >= capture.KnowledgeCutoffUtc);
        var added = 0;
        foreach (var computed in results)
        {
            var history = previous.Where(p => p.AssetId == computed.Target.AssetId && p.HorizonHours == computed.Target.HorizonHours).OrderBy(p => p.Sequence).ToArray();
            // Do not append a stale competing capture after a newer assessment has won.
            if (history.LastOrDefault() is { } latest && latest.KnowledgeCutoffUtc > capture.KnowledgeCutoffUtc) continue;
            if (history.LastOrDefault() is { } sameTime && sameTime.KnowledgeCutoffUtc == capture.KnowledgeCutoffUtc &&
                !OutcomeCalculator.IncludesEvidence(computed.Evidence, (await ReadAssessmentAsync(write, original, sameTime, cancellationToken)).Result.Evidence)) continue;
            StoredOutcome? complete = null;
            if (history.FirstOrDefault(p => p.State == "complete") is { } first) complete = await ReadAssessmentAsync(write, original, first, cancellationToken);
            var result = OutcomeCalculator.PreserveCompleted(computed, complete);
            var json = CanonicalJson.Write(result); var hash = CanonicalJson.Hash(json);
            if (history.Any(p => p.EvidenceHash == hash)) continue;
            await VerifyEvidenceAsync(write, result.Evidence, capture.KnowledgeCutoffUtc, cancellationToken);
            var id = CanonicalJson.Hash(CanonicalJson.Write(new { result.Target, hash }));
            write.Add(new ForwardOutcomeAssessmentRow { Id = id, IssuanceId = issuanceId, AssetId = result.Target.AssetId,
                HorizonHours = result.Target.HorizonHours, Sequence = history.Length + 1, AssessedAtUtc = assessed, KnowledgeCutoffUtc = capture.KnowledgeCutoffUtc,
                State = result.State, ReferencePrice = result.ReferencePrice, TerminalPrice = result.TerminalPrice, Return = result.Return,
                OriginalCompleteAssessmentId = result.OriginalCompleteAssessmentId, ResultJson = json, EvidenceHash = hash });
            await write.SaveChangesAsync(cancellationToken);
            foreach (var fact in result.Evidence.Facts)
            {
                var o = fact.Observation;
                write.Add(new ForwardOutcomeInputRow { AssessmentId = id, InstrumentId = o.InstrumentId, Kind = o.Kind, EventTimeUtc = o.EventTimeUtc,
                    PeriodSeconds = o.PeriodSeconds, PayloadId = fact.PayloadId, FactJson = CanonicalJson.Write(fact) });
            }
            added++;
        }
        await write.SaveChangesAsync(cancellationToken);
        await publication.CommitAsync(cancellationToken);
        return added;
    }

    internal static async Task VerifyEvidenceAsync(ResearchDbContext db, OutcomeEvidence evidence, DateTimeOffset cutoff, CancellationToken ct)
    {
        var last = evidence.Facts.Length == 0 ? cutoff : evidence.Facts.Max(f => f.Observation.EventTimeUtc);
        await ScoringStore.VerifySourceFactsAsync(db, new(last, cutoff, [], evidence.Facts, evidence.Conflicts), ct);
        var revisionIds = evidence.Revisions.Select(r => r.Id).ToArray();
        var revisions = await db.Set<ForwardObservationConflictRow>().AsNoTracking().Where(c => revisionIds.Contains(c.Id) && c.DetectedAtUtc <= cutoff).ToArrayAsync(ct);
        Require(revisions.Length == revisionIds.Length);
        foreach (var revision in revisions)
        {
            var payload = await db.Payloads.AsNoTracking().SingleAsync(p => p.Id == revision.CandidatePayloadId, ct);
            var original = CanonicalJson.Read<ObservationFact>(revision.OriginalFactJson);
            var candidate = CanonicalJson.Read<Observation>(revision.CandidateJson);
            Require(evidence.Conflicts.Any(c => c.Id == revision.QuarantineId) &&
                CanonicalJson.Write(evidence.Revisions.Single(r => r.Id == revision.Id)) == CanonicalJson.Write(Revision(revision, payload)) &&
                payload.InstrumentId == revision.InstrumentId && payload.IngestedAtUtc <= cutoff &&
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(payload.Bytes)) == payload.Sha256 &&
                ObservationKey.Of(candidate) == ObservationKey.Of(original.Observation) && candidate != original.Observation &&
                candidate.InstrumentId == revision.InstrumentId && candidate.Kind == revision.Kind &&
                candidate.EventTimeUtc == revision.EventTimeUtc && candidate.PeriodSeconds == revision.PeriodSeconds);
            await ScoringStore.VerifySourceFactsAsync(db, new(candidate.EventTimeUtc, cutoff, [], [original], []), ct);
        }
    }
    private static OutcomeRevision Revision(ForwardObservationConflictRow row, PayloadRow payload) =>
        new(row.Id, row.QuarantineId, CanonicalJson.Read<ObservationFact>(row.OriginalFactJson), CanonicalJson.Read<Observation>(row.CandidateJson),
            payload.Id, payload.MappingVersion, payload.Sha256, row.DetectedAtUtc);

    internal static async Task<StoredOutcome> ReadAssessmentAsync(ResearchDbContext db, ForwardOriginal original, ForwardOutcomeAssessmentRow row, CancellationToken ct)
    {
        Require(CanonicalJson.Hash(row.ResultJson) == row.EvidenceHash && row.KnowledgeCutoffUtc <= row.AssessedAtUtc);
        var result = CanonicalJson.Read<OutcomeResult>(row.ResultJson);
        Require(result.Target.IssuanceId == original.Id && result.Target.AssetId == row.AssetId && result.Target.HorizonHours == row.HorizonHours &&
            result.State == row.State && result.ReferencePrice == row.ReferencePrice && result.TerminalPrice == row.TerminalPrice && result.Return == row.Return &&
            result.OriginalCompleteAssessmentId == row.OriginalCompleteAssessmentId && ForwardMethodology.Targets(original).Contains(result.Target));
        await VerifyEvidenceAsync(db, result.Evidence, row.KnowledgeCutoffUtc, ct);
        var links = await db.Set<ForwardOutcomeInputRow>().AsNoTracking().Where(i => i.AssessmentId == row.Id).ToArrayAsync(ct);
        Require(links.Length == result.Evidence.Facts.Length);
        foreach (var fact in result.Evidence.Facts) Require(links.Any(l => l.InstrumentId == fact.Observation.InstrumentId && l.Kind == fact.Observation.Kind &&
            l.EventTimeUtc == fact.Observation.EventTimeUtc && l.PeriodSeconds == fact.Observation.PeriodSeconds && l.PayloadId == fact.PayloadId && l.FactJson == CanonicalJson.Write(fact)));
        var recomputed = OutcomeCalculator.Calculate(result.Target, original.Instruments.Single(i => i.Id == result.Target.InstrumentId),
            new(row.KnowledgeCutoffUtc, result.Evidence.Facts, result.Evidence.Conflicts, result.Evidence.Revisions));
        if (row.OriginalCompleteAssessmentId is not null)
        {
            var first = await db.Set<ForwardOutcomeAssessmentRow>().AsNoTracking().SingleAsync(a => a.Id == row.OriginalCompleteAssessmentId, ct);
            Require(first.State == "complete" && first.Sequence < row.Sequence);
            recomputed = OutcomeCalculator.PreserveCompleted(recomputed, await ReadAssessmentAsync(db, original, first, ct));
        }
        Require(CanonicalJson.Write(recomputed) == row.ResultJson);
        return new(row.Id, row.AssessedAtUtc, row.KnowledgeCutoffUtc, row.EvidenceHash, result);
    }
}
