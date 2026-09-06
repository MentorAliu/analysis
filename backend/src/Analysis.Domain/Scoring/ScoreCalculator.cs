namespace Analysis.Domain.Scoring;

public sealed class ScoreCalculator(ScoringModel model)
{
    public ScoreResult Calculate(FeatureSet features)
    {
        var m = model.Manifest; var math = new DecimalMath(m.Numeric);
        if (features.ModelId != m.ModelId || !m.Universe.Contains(features.AssetId, StringComparer.Ordinal) ||
            !features.Values.Select(f => f.Id).SequenceEqual(m.Features.Select(f => f.Id)))
            throw new ArgumentException("Feature set does not match manifest.");
        foreach (var f in features.Values)
        {
            var definition = m.Features.Single(d => d.Id == f.Id);
            var unit = definition.Unit == "base-asset"
                ? m.BaseUnits[features.AssetId] : definition.Unit;
            if (f.Key != definition.Key || f.CalculationVersion != m.FeatureVersion ||
                f.Unit != unit ||
                (f.State == "available") != f.Value.HasValue ||
                !new[] { "available", "missing", "stale", "invalid", "conflicted", "inapplicable" }.Contains(f.State, StringComparer.Ordinal) ||
                (definition.Applicability == "alts" && features.AssetId == "bitcoin") != (f.State == "inapplicable"))
                throw new ArgumentException("Invalid feature state.");
            if (f.Value.HasValue) ExactDecimal.Require(f.Value.Value);
        }
        var profile = m.Profiles[features.AssetId == "bitcoin" ? "bitcoin" : "alts"];
        var evidence = new List<EvidenceValue>();
        foreach (var c in profile)
            foreach (var group in c.Groups)
                foreach (var id in group.FeatureIds)
                {
                    var f = features.Values.Single(f => f.Id == id); var definition = m.Features.Single(d => d.Id == id);
                    decimal? normalized = null; var state = f.State;
                    if (f.State == "available")
                    {
                        try
                        {
                            normalized = definition.Normalization switch
                            {
                                "clip" => math.Clip(f.Value!.Value, definition.Threshold!.Value),
                                "negative-clip" => -math.Clip(f.Value!.Value, definition.Threshold!.Value),
                                "oi-confirmation" => OiConfirmation(f, definition, features, m, math),
                                _ => throw new InvalidOperationException("Unsupported evidence normalization.")
                            };
                            if (normalized.HasValue) normalized = math.Round(normalized.Value);
                            else state = "missing-confirmation";
                        }
                        catch (ArithmeticException) { state = "invalid-normalization"; }
                    }
                    evidence.Add(new(id, c.Category,
                        checked((int)(m.Numeric.WeightDenominator * c.Weight * group.Weight / group.FeatureIds.Length)),
                        m.Numeric.WeightDenominator, normalized, state));
                }
        var ordered = evidence.OrderBy(e => e.FeatureId).ToArray();
        var usable = ordered.Where(e => e.Normalized.HasValue).ToArray();
        var mass = usable.Sum(e => e.WeightNumerator);
        var quality = math.Final(math.Divide(100m * mass, m.Numeric.WeightDenominator));
        var ready = features.CorePriceReady && 100m * mass >= m.History.MinimumQuality * m.Numeric.WeightDenominator;
        var context = features.Values.Where(f => m.Features.Single(d => d.Id == f.Id).Normalization == "context" && f.State != "inapplicable").ToArray();
        var categories = new[] { "price", "derivatives", "fundamentals", "regime" }.Select(name =>
        {
            var all = ordered.Where(e => e.Category == name).ToArray(); var used = all.Where(e => e.Normalized.HasValue).ToArray();
            var total = all.Sum(e => e.WeightNumerator); var available = used.Sum(e => e.WeightNumerator);
            return new CategoryScore(name, total == 0 ? "inapplicable" : available == 0 ? "missing" : available == total ? "complete" : "partial",
                available == 0 ? null : math.Final(math.Divide(100m * math.Sum(used.Select(e => e.WeightNumerator * e.Normalized!.Value)), available)),
                total == 0 ? 0 : math.Final(math.Divide(100m * available, total)), total, available);
        }).ToArray();
        return new(features.AssetId, features.AsOfUtc, features.ModelId,
            !ready ? "not-ready" : mass == m.Numeric.WeightDenominator && context.All(f => f.State == "available") ? "complete" : "partial",
            ready ? math.Final(math.Divide(100m * math.Sum(usable.Select(e => e.WeightNumerator * e.Normalized!.Value)), mass)) : null,
            ready ? math.Final(math.Divide(100m * math.Sum(usable.Select(e => e.WeightNumerator * Math.Max(e.Normalized!.Value, 0))), m.Numeric.WeightDenominator)) : null,
            ready ? math.Final(math.Divide(100m * math.Sum(usable.Select(e => e.WeightNumerator * Math.Max(-e.Normalized!.Value, 0))), m.Numeric.WeightDenominator)) : null,
            quality, math.Final(math.Divide(100m * context.Count(f => f.State == "available"), context.Length)), "unassessed-single-source",
            categories, ordered);
    }

    private static decimal? OiConfirmation(FeatureValue value, FeatureDefinition definition, FeatureSet features, ScoringManifest m, DecimalMath math)
    {
        var confirmation = features.Values.Single(f => f.Id == definition.ConfirmationFeatureId);
        if (confirmation.State != "available") return null;
        var price = m.Features.Single(f => f.Id == definition.ConfirmationFeatureId);
        return checked(math.Clip(confirmation.Value!.Value, price.Threshold!.Value) *
            Math.Min(1, math.Divide(Math.Max(value.Value!.Value, 0), definition.Threshold!.Value)));
    }
}
