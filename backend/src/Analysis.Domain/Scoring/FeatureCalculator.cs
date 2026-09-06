namespace Analysis.Domain.Scoring;

public sealed class FeatureCalculator(ScoringModel model)
{
    public FeatureSet Calculate(string assetId, ScoringInput input)
    {
        var manifest = model.Manifest;
        if (!manifest.Universe.Contains(assetId, StringComparer.Ordinal)) throw new ArgumentException("Unknown scoring asset.");
        Utc.Require(input.AsOfUtc); Utc.Require(input.KnowledgeCutoffUtc);
        if (input.AsOfUtc.ToUnixTimeMilliseconds() % 3_600_000 != 0 || input.KnowledgeCutoffUtc < input.AsOfUtc)
            throw new ArgumentException("Invalid scoring clock.");
        var values = manifest.Features.Select(definition => CalculateOne(assetId, input, manifest, definition)).ToArray();
        var core = new Evaluation(input, assetId, manifest);
        var coreReady = true;
        try { core.Candles(assetId, input.AsOfUtc, manifest.History.CorePriceHours + 1); }
        catch (FeatureUnavailable) { coreReady = false; }
        return new(assetId, input.AsOfUtc, manifest.ModelId, coreReady, values);
    }

    private static FeatureValue CalculateOne(string assetId, ScoringInput input, ScoringManifest manifest, FeatureDefinition definition)
    {
        var evaluation = new Evaluation(input, assetId, manifest);
        var state = "available"; var reason = "ok"; decimal? value = null;
        var unit = definition.Unit == "base-asset"
            ? input.Instruments.Single(i => i.AssetId == assetId && i.Kind == InstrumentKind.LinearPerpetual).BaseUnit
            : definition.Unit;
        if (definition.Applicability == "alts" && assetId == "bitcoin")
        { state = "inapplicable"; reason = "asset-applicability"; }
        else
        {
            try
            {
                value = evaluation.Math.Round(evaluation.Compute(definition));
                ExactDecimal.Require(value.Value);
            }
            catch (FeatureUnavailable error) { state = error.State; reason = error.Message; value = null; }
            catch (Exception error) when (error is ArithmeticException or FormatException)
            { state = "invalid"; reason = error is DivideByZeroException ? "zero-denominator" : "numeric-contract"; value = null; }
        }
        var keys = evaluation.Used.Distinct().OrderBy(k => k.InstrumentId, StringComparer.Ordinal)
            .ThenBy(k => k.Kind).ThenBy(k => k.EventTimeUtc).ThenBy(k => k.PeriodSeconds).ToArray();
        var first = evaluation.AnchorStart ?? (keys.Length == 0 ? (DateTimeOffset?)null : keys.Min(k => k.EventTimeUtc));
        var last = evaluation.AnchorEnd ?? (keys.Length == 0 ? (DateTimeOffset?)null : keys.Max(k => k.EventTimeUtc));
        return new(definition.Id, definition.Key, manifest.FeatureVersion, unit, state, reason, value,
            keys, evaluation.Conflicts.Distinct().Order(StringComparer.Ordinal).ToArray(), evaluation.Windows.ToArray(),
            first, last, first.HasValue && last.HasValue ? (last.Value - first.Value).Ticks / TimeSpan.TicksPerMillisecond : null);
    }

    private sealed class FeatureUnavailable(string state, string reason) : Exception(reason)
    { public string State { get; } = state; }

    private sealed class Evaluation(ScoringInput input, string assetId, ScoringManifest manifest)
    {
        public DecimalMath Math { get; } = new(manifest.Numeric);
        public List<ObservationKey> Used { get; } = [];
        public List<string> Conflicts { get; } = [];
        public List<InputWindow> Windows { get; } = [];
        public DateTimeOffset? AnchorStart { get; private set; }
        public DateTimeOffset? AnchorEnd { get; private set; }

        public decimal Compute(FeatureDefinition f)
        {
            var t = input.AsOfUtc;
            return f.Operation switch
            {
                "close" => Candles(assetId, t, 1)[0].Close!.Value,
                "quote-volume" => Volume(t, f.Hours),
                "quote-volume-change" => Math.Change(Volume(t, f.Hours), Volume(t.AddHours(-f.Hours), f.Hours)),
                "return" => Return(assetId, t, f.Hours),
                "relative-strength" => Math.Change(checked(1 + Return(assetId, t, f.Hours)), checked(1 + Return("bitcoin", t, f.Hours))),
                "btc-return" => Return("bitcoin", t, f.Hours),
                "moving-average-distance" => MovingAverage(t, f.Hours),
                "realized-volatility" => Volatility(t, f.Hours),
                "funding-last" => LastFunding(t),
                "funding-sum" => Funding(t, f.Hours),
                "funding-change" => checked(Funding(t, f.Hours) - Funding(t.AddHours(-f.Hours), f.Hours)),
                "oi" => OpenInterest(t, 0)[0].Value!.Value,
                "oi-change" => OiChange(t, f.Hours),
                "tvl" => Tvl(t, 0),
                "tvl-change" => Tvl(t, f.Hours),
                _ => throw new InvalidOperationException("Unsupported feature calculation.")
            };
        }

        private Observation[] Read(string asset, InstrumentKind instrumentKind, ObservationKind kind,
            DateTimeOffset start, DateTimeOffset end, bool inclusive, string rule)
        {
            var instrument = input.Instruments.Single(i => i.AssetId == asset && i.Kind == instrumentKind);
            Windows.Add(new(instrument.Id, kind, start, end, inclusive, rule));
            var facts = input.Observations.Where(f => f.Observation.InstrumentId == instrument.Id &&
                f.Observation.Kind == kind && f.Observation.EventTimeUtc >= start &&
                (inclusive ? f.Observation.EventTimeUtc <= end : f.Observation.EventTimeUtc < end) &&
                f.IngestedAtUtc <= input.KnowledgeCutoffUtc && f.Observation.EventTimeUtc <= input.AsOfUtc &&
                (kind != ObservationKind.Candle || f.Observation.EventTimeUtc.AddSeconds(f.Observation.PeriodSeconds) <= input.AsOfUtc))
                .OrderBy(f => f.Observation.EventTimeUtc).ToArray();
            Used.AddRange(facts.Select(f => ObservationKey.Of(f.Observation)));
            var conflicts = input.Conflicts.Where(c => c.InstrumentId == instrument.Id && c.Code == "conflicting-observation" &&
                c.IngestedAtUtc <= input.KnowledgeCutoffUtc && c.EndUtc > start &&
                (inclusive ? c.StartUtc <= end : c.StartUtc < end)).ToArray();
            Conflicts.AddRange(conflicts.Select(c => c.Id));
            if (conflicts.Length != 0) throw new FeatureUnavailable("conflicted", "observation-conflict");
            try
            {
                foreach (var fact in facts)
                {
                    fact.Observation.Validate(instrument); Utc.Require(fact.IngestedAtUtc);
                    if (fact.IngestedAtUtc < fact.Observation.EventTimeUtc ||
                        kind == ObservationKind.Candle && fact.IngestedAtUtc < fact.Observation.EventTimeUtc.AddHours(1))
                        throw new ArgumentException("Invalid ingestion time.");
                }
                if (facts.Select(f => ObservationKey.Of(f.Observation)).Distinct().Count() != facts.Length)
                    throw new ArgumentException("Duplicate input keys.");
            }
            catch (Exception error) when (error is ArgumentException or FormatException)
            { throw new FeatureUnavailable("invalid", "observation-contract"); }
            return facts.Select(f => f.Observation).ToArray();
        }

        public Observation[] Candles(string asset, DateTimeOffset end, int count)
        {
            var start = end.AddHours(-count);
            var values = Read(asset, InstrumentKind.Spot, ObservationKind.Candle, start, end, false, "consecutive-closed-hourly-bars");
            if (values.Length != count || values.Where((v, i) => v.EventTimeUtc != start.AddHours(i)).Any())
                throw new FeatureUnavailable("missing", "incomplete-candle-window");
            return values;
        }
        private decimal Return(string asset, DateTimeOffset t, int hours)
        {
            var candles = Candles(asset, t, hours + 1);
            return Math.Change(candles[^1].Close!.Value, candles[0].Close!.Value);
        }
        private decimal Volume(DateTimeOffset t, int hours) => Math.Sum(Candles(assetId, t, hours).Select(c => c.QuoteVolume!.Value));
        private decimal MovingAverage(DateTimeOffset t, int hours)
        {
            var candles = Candles(assetId, t, hours);
            return Math.Change(candles[^1].Close!.Value, Math.Divide(Math.Sum(candles.Select(c => c.Close!.Value)), hours));
        }
        private decimal Volatility(DateTimeOffset t, int hours)
        {
            var candles = Candles(assetId, t, hours + 1);
            var returns = Enumerable.Range(1, hours).Select(i => Math.Change(candles[i].Close!.Value, candles[i - 1].Close!.Value));
            return Math.Sqrt(Math.Sum(returns.Select(r => checked(r * r))));
        }
        private Observation[] OpenInterest(DateTimeOffset t, int hours)
        {
            var start = t.AddHours(-hours);
            var values = Read(assetId, InstrumentKind.LinearPerpetual, ObservationKind.OpenInterestBothSides,
                start, t, true, "exact-consecutive-hourly-samples");
            if (values.Length != hours + 1 || values.Where((v, i) => v.EventTimeUtc != start.AddHours(i)).Any())
                throw new FeatureUnavailable("missing", "incomplete-oi-window");
            return values;
        }
        private decimal OiChange(DateTimeOffset t, int hours)
        { var values = OpenInterest(t, hours); return Math.Change(values[^1].Value!.Value, values[0].Value!.Value); }

        private decimal LastFunding(DateTimeOffset t)
        {
            var values = Read(assetId, InstrumentKind.LinearPerpetual, ObservationKind.FundingRate,
                t.AddHours(-manifest.History.FundingGapHours), t, true, "last-settlement-within-age-policy");
            if (values.Length == 0) throw new FeatureUnavailable("stale", "no-recent-funding");
            AnchorStart = AnchorEnd = values[^1].EventTimeUtc;
            return values[^1].Value!.Value;
        }
        private decimal Funding(DateTimeOffset t, int hours)
        {
            var start = t.AddHours(-hours); var gap = TimeSpan.FromHours(manifest.History.FundingGapHours);
            var values = Read(assetId, InstrumentKind.LinearPerpetual, ObservationKind.FundingRate,
                start - gap, t, true, "settlement-events-with-boundary-and-gap-guards");
            var predecessor = values.LastOrDefault(v => v.EventTimeUtc <= start);
            var events = values.Where(v => v.EventTimeUtc > start).ToArray();
            if (predecessor is null || events.Length == 0) throw new FeatureUnavailable("missing", "funding-boundary-coverage");
            if (t - events[^1].EventTimeUtc > gap) throw new FeatureUnavailable("stale", "funding-last-event-age");
            var previous = predecessor.EventTimeUtc;
            foreach (var item in events)
            {
                if (item.EventTimeUtc - previous > gap) throw new FeatureUnavailable("missing", "funding-event-gap");
                previous = item.EventTimeUtc;
            }
            return Math.Sum(events.Select(v => v.Value!.Value));
        }
        private decimal Tvl(DateTimeOffset t, int hours)
        {
            var start = t.AddHours(-hours); var gap = TimeSpan.FromHours(manifest.History.TvlGapHours);
            var values = Read(assetId, InstrumentKind.Chain, ObservationKind.ChainTvl, start - gap, t, true,
                "latest-at-or-before-targets-with-age-and-gap-guards");
            var current = values.LastOrDefault();
            if (current is null || t - current.EventTimeUtc > gap) throw new FeatureUnavailable("stale", "tvl-current-age");
            if (hours == 0)
            { AnchorStart = AnchorEnd = current.EventTimeUtc; return current.Value!.Value; }
            var baseline = values.LastOrDefault(v => v.EventTimeUtc <= start);
            if (baseline is null || start - baseline.EventTimeUtc > gap)
                throw new FeatureUnavailable("missing", "tvl-baseline-coverage");
            if (baseline.EventTimeUtc == current.EventTimeUtc) throw new FeatureUnavailable("missing", "tvl-identical-anchors");
            var previous = baseline.EventTimeUtc;
            foreach (var item in values.Where(v => v.EventTimeUtc > baseline.EventTimeUtc))
            {
                if (item.EventTimeUtc - previous > gap) throw new FeatureUnavailable("missing", "tvl-observation-gap");
                previous = item.EventTimeUtc;
            }
            AnchorStart = baseline.EventTimeUtc; AnchorEnd = current.EventTimeUtc;
            return Math.Change(current.Value!.Value, baseline.Value!.Value);
        }
    }
}
