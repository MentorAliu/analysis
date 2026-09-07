using Analysis.Domain.Scoring;

namespace Analysis.Domain.SignalsOutcomes;

public static class OutcomeCalculator
{
    // Snapshot clocks have millisecond precision. Equal cutoffs can still describe
    // different committed evidence; an older capture must not erase known facts.
    public static bool IncludesEvidence(OutcomeEvidence candidate, OutcomeEvidence known) =>
        known.Facts.All(f => candidate.Facts.Any(c => ObservationKey.Of(c.Observation) == ObservationKey.Of(f.Observation) && c.PayloadId == f.PayloadId)) &&
        known.Conflicts.All(f => candidate.Conflicts.Any(c => c.Id == f.Id)) &&
        known.Revisions.All(f => candidate.Revisions.Any(c => c.Id == f.Id));

    public static OutcomeResult Calculate(OutcomeTarget target, InstrumentRef instrument, OutcomeCapture capture)
    {
        Utc.Require(capture.KnowledgeCutoffUtc);
        if (!ForwardMethodology.Manifest.HorizonHours.Contains(target.HorizonHours) ||
            target.ReferenceBoundaryUtc != ForwardMethodology.Hour(target.ReferenceBoundaryUtc) ||
            target.MaturityUtc != target.ReferenceBoundaryUtc.AddHours(target.HorizonHours) ||
            instrument.Id != target.InstrumentId || instrument.AssetId != target.AssetId ||
            instrument.ProviderId != "binance" || instrument.Kind != InstrumentKind.Spot || instrument.QuoteUnit != "USDT")
            throw new ArgumentException("invalid-outcome-target");
        var start = target.ReferenceBoundaryUtc.AddHours(-1);
        var map = capture.Facts.Where(f => f.Observation.InstrumentId == instrument.Id && f.Observation.Kind == ObservationKind.Candle &&
            f.Observation.EventTimeUtc >= start && f.Observation.EventTimeUtc < target.MaturityUtc &&
            f.IngestedAtUtc <= capture.KnowledgeCutoffUtc && f.Observation.EventTimeUtc.AddHours(1) <= capture.KnowledgeCutoffUtc)
            .ToDictionary(f => ObservationKey.Of(f.Observation));
        var facts = new List<ObservationFact>(); var missing = new List<ObservationKey>(); var invalid = new List<ObservationKey>();
        for (var t = start; t < target.MaturityUtc; t = t.AddHours(1))
        {
            var key = new ObservationKey(instrument.Id, ObservationKind.Candle, t, 3600);
            if (!map.TryGetValue(key, out var fact)) { missing.Add(key); continue; }
            facts.Add(fact);
            try { fact.Observation.Validate(instrument); }
            catch (Exception error) when (error is ArgumentException or FormatException) { invalid.Add(key); }
        }
        var conflicts = capture.Conflicts.Where(c => c.InstrumentId == instrument.Id && c.Code == "conflicting-observation" &&
            c.StartUtc < target.MaturityUtc && c.EndUtc > start && c.IngestedAtUtc <= capture.KnowledgeCutoffUtc)
            .OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        var revisions = (capture.Revisions ?? []).Where(r => r.Candidate.InstrumentId == instrument.Id &&
            r.Candidate.EventTimeUtc >= start && r.Candidate.EventTimeUtc < target.MaturityUtc && r.DetectedAtUtc <= capture.KnowledgeCutoffUtc)
            .OrderBy(r => r.Id, StringComparer.Ordinal).ToArray();
        var evidence = new OutcomeEvidence(facts.ToArray(), missing.ToArray(), invalid.ToArray(), conflicts, revisions);
        OutcomeResult Result(string state, string reason, decimal? p0 = null, decimal? p1 = null, decimal? value = null) =>
            new(target, state, reason, "USDT", "fraction", p0, p1, value, evidence);
        if (capture.KnowledgeCutoffUtc < target.MaturityUtc) return Result("pending", "horizon-not-mature");
        if (conflicts.Length > 0) return Result("conflicted", "conflicting-observation");
        if (missing.Count > 0 || invalid.Count > 0) return Result("incomplete", invalid.Count > 0 ? "invalid-observation" : "missing-observation");
        var reference = facts[0].Observation.Close!.Value; var terminal = facts[^1].Observation.Close!.Value;
        try { return Result("complete", "full-hourly-path", reference, terminal, ForwardMethodology.RatioReturn(terminal, reference)); }
        catch (Exception error) when (error is OverflowException or FormatException) { return Result("incomplete", "numeric-range"); }
    }

    public static OutcomeResult PreserveCompleted(OutcomeResult current, StoredOutcome? originalComplete)
    {
        if (originalComplete is null) return current;
        if (originalComplete.Result.State != "complete" || originalComplete.Result.Target != current.Target)
            throw new ArgumentException("invalid-original-outcome");
        if (current.Evidence.Conflicts.Length == 0) return originalComplete.Result;
        return originalComplete.Result with { State = "conflicted", Reason = "revision-after-completion",
            Evidence = originalComplete.Result.Evidence with { Conflicts = current.Evidence.Conflicts, Revisions = current.Evidence.Revisions },
            OriginalCompleteAssessmentId = originalComplete.Id };
    }
}
