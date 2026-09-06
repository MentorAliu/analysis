using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Analysis.Infrastructure;

namespace Analysis.ScoringChecks;

internal static class UnitChecks
{
    public static void Run()
    {
        var model = ScoringModel.Slice1; var manifest = model.Manifest;
        var math = new DecimalMath(manifest.Numeric); var calculator = new FeatureCalculator(model); var scorer = new ScoreCalculator(model);
        Check.Equal(21, manifest.Features.Length, "Feature catalog count");
        Check.Equal(64, model.Hash.Length, "Manifest SHA-256"); Check.Equal(64, model.SourceHash.Length, "Calculator source SHA-256");
        manifest.Universe[0] = "mutated";
        Check.Equal("bitcoin", model.Manifest.Universe[0], "Manifest caller mutation cannot alter registered model");
        Check.Equal("{\"a\":1,\"b\":2}", CanonicalJson.Normalize("{\"b\":2.00,\"a\":1}"), "Canonical property/numeric order");
        Check.Equal(0.047619047619047619m, math.Change(1.1m, 1.05m), "Independent rational example 1/21");
        Check.Equal(1.414213562373095049m, math.Sqrt(2m), "Independent sqrt(2) to 18 places");
        Check.Equal(0m, math.Sqrt(0m), "sqrt zero"); Check.Equal(0.1m, math.Sqrt(0.01m), "Exact sqrt fraction");
        Check.Equal(0.00000000000001m, math.Sqrt(0.0000000000000000000000000001m), "Tiny sqrt");
        Check.Equal(0.000000000000000002m, math.Round(0.0000000000000000015m), "Odd midpoint up");
        Check.Equal(0.000000000000000002m, math.Round(0.0000000000000000025m), "Even midpoint stays");
        Check.Throws<ArithmeticException>(() => math.Sqrt(-1), "Negative sqrt rejected");
        Check.Throws<DivideByZeroException>(() => math.Divide(1, 0), "Zero divisor rejected");
        Check.Throws<OverflowException>(() => math.Sum([decimal.MaxValue, 1m]), "Checked sum overflow");
        Check.Pass("Immutable 21-feature manifest, canonical hashes and independent decimal arithmetic");

        var input = Synthetic.Input(); var t = input.AsOfUtc;
        foreach (var asset in model.Manifest.Universe)
        {
            var f = calculator.Calculate(asset, input); var s = scorer.Calculate(f);
            Check.That(f.CorePriceReady && f.Values.Length == 21, "Complete core and feature count");
            Check.That(f.Values.All(v => v.State is "available" or "inapplicable"), "All applicable constant-series features available");
            Check.Equal(asset == "bitcoin" ? 7 : 0, f.Values.Count(v => v.State == "inapplicable"), "BTC explicit applicability");
            Check.Equal<decimal?>(0, s.Composite, "Neutral composite golden");
            Check.Equal<decimal?>(0, s.BullishConfidence, "Neutral bullish confidence golden");
            Check.Equal<decimal?>(0, s.BearishConfidence, "Neutral bearish confidence golden");
            Check.Equal(100m, s.DataQuality, "Complete quality golden"); Check.Equal(100m, s.ContextCoverage, "Complete context coverage");
            Check.Equal(60000, s.Evidence.Sum(e => e.WeightNumerator), "Exact rational weights sum to one");
            Check.That(f.Values.SelectMany(v => v.Inputs).All(k => k.Kind != ObservationKind.Candle || k.EventTimeUtc < t), "No open/future candle");
        }
        var eth = Synthetic.Features();
        Check.Throws<ArgumentException>(() => scorer.Calculate(eth with { Values = eth.Values.Select(f => f.Id == 5 ? f with { Unit = "percent" } : f).ToArray() }), "Scoring rejects fraction/percent unit drift");
        var maximum = Synthetic.Values(eth, (4,0.02m),(5,0.05m),(6,0.1m),(8,0.05m),(9,0.1m),(10,0.03m),
            (11,0.05m),(12,0.1m),(14,-0.003m),(15,-0.003m),(17,0.05m),(18,0.1m),(20,0.05m),(21,0.1m));
        var fullBull = scorer.Calculate(maximum);
        Check.Equal<decimal?>(100, fullBull.Composite, "All-positive score-vector golden");
        Check.Equal<decimal?>(100, fullBull.BullishConfidence, "All-positive confidence golden");
        Check.Equal<decimal?>(0, fullBull.BearishConfidence, "No opposite evidence");
        var minimum = maximum with { Values = maximum.Values.Select(f => f.Id is 17 or 18 || model.Manifest.Features.Single(d => d.Id == f.Id).Normalization == "context" ? f : f with { Value = -f.Value }).ToArray() };
        var fullBear = scorer.Calculate(minimum);
        Check.Equal<decimal?>(-100, fullBear.Composite, "All-negative score-vector golden");
        Check.Equal<decimal?>(100, fullBear.BearishConfidence, "All-negative confidence golden");
        var mixed = Synthetic.Values(maximum, (11,-0.05m),(12,-0.1m),(14,0.003m),(15,0.003m),(20,-0.05m),(21,-0.1m));
        var mixedScore = scorer.Calculate(mixed);
        // Positive price .30 + OI .15; negative funding .15 + TVL .30 + regime .10.
        Check.Equal<decimal?>(-10, mixedScore.Composite, "Independent mixed composite -10");
        Check.Equal<decimal?>(45, mixedScore.BullishConfidence, "Independent positive mass 45");
        Check.Equal<decimal?>(55, mixedScore.BearishConfidence, "Independent negative mass 55");
        var confirmationOnly = scorer.Calculate(Synthetic.Values(eth, (10,1m)));
        Check.Equal<decimal?>(1.5m, confirmationOnly.Composite, "MA only 5% of 30% price");
        Check.Equal<decimal?>(0, scorer.Calculate(Synthetic.Values(eth, (1,999),(2,999),(3,999),(7,999),(13,999),(16,999),(19,999))).Composite,
            "Context levels never drive scores");
        Check.Equal<decimal?>(0, scorer.Calculate(Synthetic.Values(eth, (17,100))).Composite, "OI expansion alone is not directional");
        Check.Equal(0m, scorer.Calculate(Synthetic.Values(maximum, (17,-1))).Evidence.Single(e => e.FeatureId == 17).Normalized, "OI contraction neutral");
        var btc = Synthetic.Features("bitcoin");
        var half = scorer.Calculate(Synthetic.Missing(btc, 14,15,17,18));
        Check.Equal(50m, half.DataQuality, "Exactly 50% threshold"); Check.That(half.Composite.HasValue, "50% permitted with complete core");
        Check.That(scorer.Calculate(Synthetic.Missing(btc, 10,14,15,17,18)).Composite is null, "Below 50% withheld");
        Check.That(scorer.Calculate(btc with { CorePriceReady = false }).Composite is null, "Incomplete core withheld despite feature coverage");
        Check.That(scorer.Calculate(Synthetic.Missing(maximum, 4)).Evidence.Single(e => e.FeatureId == 17).Normalized is null, "OI requires usable price confirmation");
        var partialBull = scorer.Calculate(Synthetic.Missing(Synthetic.Values(btc, (4,0.02m),(5,0.05m),(6,0.1m),(10,0.03m)),14,15,17,18));
        Check.Equal<decimal?>(100, partialBull.Composite, "Independent partial available-weight mean");
        Check.Equal<decimal?>(50, partialBull.BullishConfidence, "Missing half of applicable evidence halves confidence");
        Check.Equal<decimal?>(0, partialBull.BearishConfidence, "Partial confidence is not a complement");
        Check.Pass("Frozen neutral/bullish/bearish/mixed vectors, exact weights, clipping, confidence and partial eligibility");

        var changed = Synthetic.Map(input, f => f.Observation.Kind == ObservationKind.Candle && f.Observation.EventTimeUtc == t.AddHours(-1)
            ? f with { Observation = Synthetic.Price(f.Observation, f.Observation.InstrumentId.Contains("BTC", StringComparison.Ordinal) ? 105 : 110) } : f);
        var known = calculator.Calculate("ethereum", changed);
        Check.Equal<decimal?>(0.1m, known.Values.Single(f => f.Id == 5).Value, "Known 24h return");
        Check.Equal<decimal?>(0.047619047619047619m, known.Values.Single(f => f.Id == 8).Value, "Known geometric relative strength");
        Check.Equal<decimal?>(0.1m, known.Values.Single(f => f.Id == 7).Value, "Known unannualized simple-return volatility");
        Check.Equal<decimal?>(24000m, eth.Values.Single(f => f.Id == 2).Value, "Quote volume is summed in USDT");
        var gap = Synthetic.Remove(input, f => f.Observation.InstrumentId == "binance:spot:ETHUSDT" && f.Observation.EventTimeUtc == t.AddHours(-10));
        var gapFeatures = calculator.Calculate("ethereum", gap);
        Check.That(!gapFeatures.CorePriceReady && gapFeatures.Values.Single(f => f.Id == 5).State == "missing", "Middle candle hole not bridged");
        Check.Equal("available", calculator.Calculate("solana", gap).Values.Single(f => f.Id == 5).State, "Unrelated asset preserved");
        var late = Synthetic.Map(input, f => f.Observation.InstrumentId == "binance:spot:ETHUSDT" && f.Observation.EventTimeUtc == t.AddHours(-1) ? f with { IngestedAtUtc = Synthetic.K.AddMilliseconds(1) } : f);
        Check.That(!calculator.Calculate("ethereum", late).CorePriceReady, "After-cutoff late input excluded");
        var future = Synthetic.Map(input, f => f.Observation.EventTimeUtc >= t && f.Observation.Kind == ObservationKind.Candle ? f with { Observation = Synthetic.Price(f.Observation, 99999) } : f);
        Check.Equal(CanonicalJson.Write(eth), CanonicalJson.Write(calculator.Calculate("ethereum", future)), "Future and currently open candles cannot affect features");
        var badUnit = Synthetic.Map(input, f => f.Observation.InstrumentId == "bybit:linear:ETHUSDT" && f.Observation.Kind == ObservationKind.OpenInterestBothSides && f.Observation.EventTimeUtc == t ? f with { Observation = f.Observation with { Unit = "USD" } } : f);
        Check.Equal("invalid", calculator.Calculate("ethereum", badUnit).Values.Single(f => f.Id == 16).State, "Base OI must not become USD");
        var missingOi = Synthetic.Remove(input, f => f.Observation.Kind == ObservationKind.OpenInterestBothSides && f.Observation.EventTimeUtc == t);
        Check.Equal("missing", calculator.Calculate("ethereum", missingOi).Values.Single(f => f.Id == 16).State, "OI exact T required");
        var middleOi = Synthetic.Remove(input, f => f.Observation.Kind == ObservationKind.OpenInterestBothSides && f.Observation.EventTimeUtc == t.AddHours(-2));
        Check.Equal("missing", calculator.Calculate("ethereum", middleOi).Values.Single(f => f.Id == 17).State, "OI endpoints alone cannot bridge a middle gap");
        var knownOi = Synthetic.Map(input, f => f.Observation.Kind == ObservationKind.OpenInterestBothSides && f.Observation.EventTimeUtc == t ? f with { Observation = f.Observation with { Value = 1100 } } : f);
        Check.Equal<decimal?>(0.1m, calculator.Calculate("ethereum", knownOi).Values.Single(f => f.Id == 18).Value, "Independent OI growth 1100/1000 minus one");
        var zeroOi = Synthetic.Map(input, f => f.Observation.Kind == ObservationKind.OpenInterestBothSides && f.Observation.EventTimeUtc == t.AddHours(-24) ? f with { Observation = f.Observation with { Value = 0 } } : f);
        Check.Equal("invalid", calculator.Calculate("ethereum", zeroOi).Values.Single(f => f.Id == 18).State, "Zero OI denominator is invalid");
        var conflict = input with { Conflicts = [new("fixture-conflict", "binance:spot:ETHUSDT", t.AddHours(-3), t, Synthetic.K, "conflicting-observation")] };
        Check.Equal("conflicted", calculator.Calculate("ethereum", conflict).Values.Single(f => f.Id == 5).State, "Conflict quarantine frozen into features");
        Check.Equal("available", calculator.Calculate("bitcoin", conflict).Values.Single(f => f.Id == 5).State, "Conflict isolation");
        var funding = Synthetic.Map(input, f => f.Observation.Kind == ObservationKind.FundingRate ? f with { Observation = f.Observation with { Value = 0.0001m } } : f);
        Check.Equal<decimal?>(0.0003m, calculator.Calculate("ethereum", funding).Values.Single(f => f.Id == 14).Value, "Three observed prints sum to fraction, start exclusive/end inclusive");
        var noFunding = Synthetic.Remove(input, f => f.Observation.Kind == ObservationKind.FundingRate);
        Check.Equal("missing", calculator.Calculate("ethereum", noFunding).Values.Single(f => f.Id == 14).State, "No funding is missing not zero");
        var noPredecessor = Synthetic.Remove(funding, f => f.Observation.Kind == ObservationKind.FundingRate && f.Observation.EventTimeUtc <= t.AddHours(-24));
        Check.Equal("missing", calculator.Calculate("ethereum", noPredecessor).Values.Single(f => f.Id == 14).State, "Funding boundary predecessor is required");
        var fundGap = Synthetic.Remove(funding, f => f.Observation.Kind == ObservationKind.FundingRate && f.Observation.EventTimeUtc == t.AddHours(-8));
        Check.Equal("missing", calculator.Calculate("ethereum", fundGap).Values.Single(f => f.Id == 14).State, "Observed funding gap >12h rejected");
        var dynamicFunding = funding with { Observations = funding.Observations.Concat(Enumerable.Range(1,7).Select(h => funding.Observations.First(f => f.Observation.Kind == ObservationKind.FundingRate && f.Observation.InstrumentId == "bybit:linear:ETHUSDT") with
            { Observation = new("bybit:linear:ETHUSDT", ObservationKind.FundingRate, t.AddHours(-h), 0, "fraction", null, Value: 0.0001m) })).ToArray() };
        Check.Equal<decimal?>(0.001m, calculator.Calculate("ethereum", dynamicFunding).Values.Single(f => f.Id == 14).Value, "Dynamic hourly settlements summed without schedule inference");
        var staleTvl = Synthetic.Remove(input, f => f.Observation.Kind == ObservationKind.ChainTvl && f.Observation.EventTimeUtc > t.AddHours(-37));
        Check.Equal("stale", calculator.Calculate("ethereum", staleTvl).Values.Single(f => f.Id == 19).State, "TVL age limit");
        var tvlHole = Synthetic.Remove(input, f => f.Observation.Kind == ObservationKind.ChainTvl && f.Observation.EventTimeUtc == t.AddDays(-2));
        Check.Equal("missing", calculator.Calculate("ethereum", tvlHole).Values.Single(f => f.Id == 21).State, "TVL internal 48h gap rejected");
        Check.Equal<long?>(72 * 3_600_000L, eth.Values.Single(f => f.Id == 21).ElapsedMilliseconds, "TVL actual endpoint interval retained");
        var irregularTvl = Synthetic.Map(input, f => f.Observation.Kind == ObservationKind.ChainTvl && f.Observation.EventTimeUtc == t ? f with { Observation = f.Observation with { EventTimeUtc = t.AddHours(-2), Value = 1100000 } } : f);
        var irregularFeature = calculator.Calculate("ethereum", irregularTvl).Values.Single(f => f.Id == 20);
        Check.Equal<decimal?>(0.1m, irregularFeature.Value, "Independent irregular TVL growth");
        Check.Equal<long?>(22 * 3_600_000L, irregularFeature.ElapsedMilliseconds, "Irregular TVL retains actual 22h interval");
        var sameTvl = Synthetic.Remove(input, f => f.Observation.Kind == ObservationKind.ChainTvl && f.Observation.EventTimeUtc > t.AddHours(-24));
        Check.Equal("missing", calculator.Calculate("ethereum", sameTvl).Values.Single(f => f.Id == 20).State, "Identical TVL anchors are unavailable, not zero change");
        Check.Pass("Exact feature examples, history edges, funding changes, TVL ages, cutoff, units and conflict isolation");

        var now = Synthetic.K;
        string[] scoreArgs = ["--score-once","--private-use","--country","XK","--as-of-utc","2021-01-08T00:00:00Z","--knowledge-cutoff-utc","2021-01-09T00:00:00Z","--model","slice1-v1"];
        Check.That(ScoringCommand.TryParse(scoreArgs, now, out _), "Exact scoring command accepted");
        foreach (var invalid in new[] { scoreArgs[..^1], scoreArgs.Concat(new[]{"--live"}).ToArray(), scoreArgs.Select(x => x == "XK" ? "DE" : x).ToArray(), scoreArgs.Select(x => x == "slice1-v1" ? "other" : x).ToArray(), scoreArgs.Select(x => x == "2021-01-08T00:00:00Z" ? "2021-01-08T00:01:00Z" : x).ToArray() })
            Check.That(!ScoringCommand.TryParse(invalid, now, out _), "Malformed command rejected before I/O");
        Check.That(!ScoringCommand.TryParse(scoreArgs, now.AddSeconds(-1), out _), "Future cutoff rejected");
        Check.That(ScoringCommand.TryParse(["--replay-scores","--model","slice1-v1","--start-utc","2021-01-08T00:00:00Z","--end-utc","2021-01-09T00:00:00Z"],now,out _), "Replay range accepted");
        Check.Throws<OperationCanceledException>(() => ScoringJobs.Calculate(input, model, new CancellationToken(true)), "Pure batch cancellation");
        Check.Pass("Bounded worker request parsing and pure calculation cancellation");
    }
}
