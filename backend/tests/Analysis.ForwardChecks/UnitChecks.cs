using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Analysis.Domain.SignalsOutcomes;
using Analysis.Infrastructure;
using Analysis.Infrastructure.Persistence;

namespace Analysis.ForwardChecks;

internal static class UnitChecks
{
    public static void Run()
    {
        Check.Equal("acef235e40c75ed4b4aa3f430dda949c9163afdd92aab73c49a7143ee5137eb1", ScoringModel.Slice1.Hash, "Frozen M3 manifest hash");
        Check.Equal("d57997b39e15e37a40d79ed52e5fe36dec48b2f08e0506b16988a599a3747656", ScoringModel.Slice1.SourceHash, "Frozen M3 source hash");
        Check.Equal(64, ForwardMethodology.SourceHash.Length, "Separate methodology implementation digest");
        var manifest = ForwardMethodology.Manifest; manifest.HorizonHours[0] = 99;
        Check.Equal(1, ForwardMethodology.Manifest.HorizonHours[0], "Methodology cannot be mutated by caller");
        var t = Synthetic.T; var k = Synthetic.K;
        ForwardMethodology.RequireIssuanceClock(t, k, k, t.AddMinutes(15));
        Check.Throws<ArgumentException>(() => ForwardMethodology.RequireIssuanceClock(t, k, k, t.AddMinutes(15).AddMilliseconds(1)), "Freshness deadline");
        Check.Throws<ArgumentException>(() => ForwardMethodology.RequireIssuanceClock(t, t.AddMilliseconds(-1), k, k), "Cutoff cannot precede as-of");
        Check.Throws<ArgumentException>(() => ForwardMethodology.RequireIssuanceClock(t, k, k.AddMilliseconds(1), k), "Clock reversal");
        Check.Throws<ArgumentException>(() => ForwardMethodology.Hour(t.ToOffset(TimeSpan.FromHours(1))), "UTC required");
        Check.Equal(0.1m, ForwardMethodology.RatioReturn(110, 100), "Independent gain vector");
        Check.Equal(-0.2m, ForwardMethodology.RatioReturn(80, 100), "Independent loss vector");
        Check.Equal(0m, ForwardMethodology.RatioReturn(100, 100), "Measured zero");
        Check.Equal(-0.666666666666666667m, ForwardMethodology.RatioReturn(1, 3), "Repeating fraction");
        Check.Equal(0.000000000000000002m, ForwardMethodology.Round(0.0000000000000000015m), "Odd midpoint");
        Check.Equal(0.000000000000000002m, ForwardMethodology.Round(0.0000000000000000025m), "Even midpoint");
        Check.Throws<ArgumentException>(() => ForwardMethodology.RatioReturn(1, 0), "Positive reference");
        Check.Throws<OverflowException>(() => ForwardMethodology.RatioReturn(decimal.MaxValue, 0.000000000000000001m), "Overflow cannot be coerced");
        var original = Synthetic.Original();
        Check.Equal(3, original.Items.Length, "Full original universe");
        Check.Equal("bitcoin,ethereum,solana", string.Join(',', original.Items.Select(i => i.Asset.Id)), "Exact ties use canonical ordinal IDs");
        Check.That(original.Items.Select(i => i.Rank).SequenceEqual(new int?[] { 1, 2, 3 }), "Unique ranks");
        Check.Equal(t.AddHours(1), original.ReferenceBoundaryUtc, "First boundary strictly after issuance");
        Check.Equal(12, ForwardMethodology.Targets(original).Length, "All assets and four horizons");
        var allMissing = ScoringJobs.Calculate(Synthetic.Input() with { Observations = [] }, ScoringModel.Slice1);
        var empty = Synthetic.Original(allMissing);
        Check.That(empty.EligibleAssetIds.Length == 0 && empty.Items.All(i => i.Rank is null), "All-not-ready issuance retained");
        Check.Equal("unavailable", empty.RelativeStrength.State, "Empty benchmark retained");
        var mixed = ScoringJobs.Calculate(Synthetic.Input(), ScoringModel.Slice1);
        mixed = mixed with { Assets = mixed.Assets.Select(a => a with { Score = a.Score with { Composite = a.Score.AssetId == "ethereum" ? -0.000001m : -0.000002m } }).ToArray() };
        var negative = Synthetic.Original(mixed);
        Check.Equal("ethereum", negative.Items[0].Asset.Id, "Negative ranks preserved with precise comparison");
        Check.Equal(3, negative.EligibleAssetIds.Length, "No positive-score filter");
        foreach (var target in ForwardMethodology.Targets(original))
        {
            var instrument = CatalogSeed.Instruments.Single(i => i.Id == target.InstrumentId);
            var capture = Synthetic.Outcome(target);
            var complete = OutcomeCalculator.Calculate(target, instrument, capture);
            Check.Equal("complete", complete.State, "All-horizon maturity boundary");
            Check.Equal<decimal?>(0.1m, complete.Return, "All-horizon independent return");
            Check.Equal(target.HorizonHours + 1, complete.Evidence.Facts.Length, "Full H+1 path");
            Check.Equal("pending", OutcomeCalculator.Calculate(target, instrument, capture with { KnowledgeCutoffUtc = target.MaturityUtc.AddMilliseconds(-1) }).State, "Before exact maturity");
            foreach (var missingIndex in new[] { 0, target.HorizonHours, target.HorizonHours / 2 }.Distinct())
                Check.Equal("incomplete", OutcomeCalculator.Calculate(target, instrument, capture with { Facts = capture.Facts.Where((_, n) => n != missingIndex).ToArray() }).State, "Reference/interior/terminal gaps");
            Check.Throws<ArgumentException>(() => OutcomeCalculator.Calculate(target, instrument with { QuoteUnit = "USD" }, capture), "No USDT/USD substitution");
            var late = capture with { KnowledgeCutoffUtc = capture.KnowledgeCutoffUtc.AddDays(1), Facts = capture.Facts.Select(f => f with { IngestedAtUtc = f.IngestedAtUtc.AddDays(1) }).ToArray() };
            Check.Equal("complete", OutcomeCalculator.Calculate(target, instrument, late).State, "Late first facts allowed");
            var conflict = new ConflictFact("conflict", instrument.Id, target.ReferenceBoundaryUtc.AddHours(-1), target.ReferenceBoundaryUtc, capture.KnowledgeCutoffUtc, "conflicting-observation");
            var disputed = OutcomeCalculator.Calculate(target, instrument, capture with { Conflicts = [conflict] });
            Check.Equal("conflicted", disputed.State, "Conflict invalidates path");
            var preserved = OutcomeCalculator.PreserveCompleted(disputed, new("first", capture.KnowledgeCutoffUtc, capture.KnowledgeCutoffUtc, "hash", complete));
            Check.Equal(complete.Return, preserved.Return, "Revision preserves original outcome");
            Check.Equal("first", preserved.OriginalCompleteAssessmentId, "Original measurement lineage");
        }
        var btc = CatalogSeed.Instruments.Single(i => i.Id == "binance:spot:BTCUSDT");
        var firstTarget = ForwardMethodology.Targets(original)[0];
        var bad = Synthetic.Outcome(firstTarget);
        bad = bad with { Facts = bad.Facts.Select((f, n) => n == 0 ? f with { Observation = f.Observation with { QuoteUnit = "USD" } } : f).ToArray() };
        Check.Equal("incomplete", OutcomeCalculator.Calculate(firstTarget, btc, bad).State, "Wrong quote fact not substituted");
        var prefix = new[] { "--issue-forward-once", "--private-use", "--country", "XK", "--as-of-utc", "2021-01-08T00:00:00Z", "--model", "slice1-v1" };
        Check.That(ForwardCommand.TryParse(prefix, out _), "Explicit issuer grammar");
        Check.That(!ForwardCommand.TryParse(prefix.Concat(new[] { "--issued-at", "2021-01-08T00:00:00Z" }).ToArray(), out _), "Cannot supply issuance timestamp");
        Check.That(!ForwardCommand.TryParse(prefix.Select(s => s == "XK" ? "US" : s).ToArray(), out _), "Country boundary");
        Check.That(!ForwardCommand.TryParse(["--issue-forward-once"], out _), "Malformed command does no I/O");
        var outcomes = ForwardMethodology.Targets(original).Select(target =>
        {
            var instrument = CatalogSeed.Instruments.Single(i => i.Id == target.InstrumentId);
            var result = OutcomeCalculator.Calculate(target, instrument, Synthetic.Outcome(target, target.AssetId == "bitcoin" ? 110 : target.AssetId == "ethereum" ? 80 : 100));
            return new StoredOutcome("fixture:" + target.AssetId + target.HorizonHours, target.MaturityUtc, target.MaturityUtc, "hash", result);
        }).ToArray();
        var report = BenchmarkCalculator.Aggregate([new(original, outcomes, outcomes, [])]);
        Check.Equal(4, report.Length, "Predefined horizons all reported");
        Check.That(report.All(r => r.PairedCompleteSamples == 1 && r.ModelMinusBitcoinMean == 0 && r.MeanReturnByRank[2] == -0.2m), "Matched means preserve unfavorable returns");
        var noData = BenchmarkCalculator.Aggregate([new(original, [], [], ForwardMethodology.Targets(original))]);
        Check.That(noData.All(r => r.BitcoinMean is null && r.PairedCompleteSamples == 0 && r.TotalIssuances == 1), "No-data denominator and null means");
        Check.That(report.All(r => r.CompleteSamplesByRank.Values.All(n => n == 1) && r.OutcomeStateCounts["complete"] == 3), "Rank and coverage denominators explicit");
        var sparse = BenchmarkCalculator.Aggregate([new(original, outcomes.Where(o => o.Result.Target.AssetId == "ethereum").ToArray(), [], [])]);
        Check.That(sparse.All(r => r.PairedCompleteSamples == 0 && r.CompleteSamplesByRank[2] == 1 && r.CompleteSamplesByRank[1] == 0 && r.OutcomeStateCounts["unassessed"] == 2), "Individual rank samples never silently become paired cohorts");
        var changedFeatures = ScoringJobs.Calculate(Synthetic.Input(), ScoringModel.Slice1);
        changedFeatures = changedFeatures with { Assets = changedFeatures.Assets.Select(a => a with { Features = a.Features with {
            Values = a.Features.Values.Select(f => f.Id == 5 ? f with { Value = a.Features.AssetId == "bitcoin" ? 0.25m : a.Features.AssetId == "ethereum" ? 0.5m : -0.5m } : f).ToArray() } }).ToArray() };
        var rs = Synthetic.Original(changedFeatures).RelativeStrength;
        Check.Equal("ethereum", rs.Items[0].AssetId, "Relative-strength ordering fixed independently of composite");
        Check.Equal(0.2m, rs.Items[0].Value, "RS ratio uses gross returns, not subtraction");
        Check.Equal(-0.6m, rs.Items[2].Value, "Negative relative strength preserved");
        var unavailable = original with { RelativeStrength = new("unavailable", "missing-bitcoin-return", []) };
        Check.That(BenchmarkCalculator.Aggregate([new(unavailable, outcomes, [], [])]).All(r => r.Exclusions["benchmark-unavailable"] == 1 && r.PairedCompleteSamples == 0), "Missing frozen benchmark never backfilled from outcomes");
        var second = original with { Id = "second" };
        var third = original with { Id = "third" };
        var huge = outcomes.Select(o => o with { Result = o.Result with { Return = 100000000000000000000m } }).ToArray();
        var zero = outcomes.Select(o => o with { Result = o.Result with { Return = 0m } }).ToArray();
        var overflowMean = BenchmarkCalculator.Aggregate([new(original, huge, [], []), new(second, zero, [], []), new(third, zero, [], [])]);
        Check.That(overflowMean.All(r => r.ModelTop1Mean is null && r.AggregationIssues["model"] == "numeric-range" && r.PairedCompleteSamples == 3), "Unrepresentable mean disclosed without dropping its denominator");
        Check.Pass("Independent numeric/time/identity/readiness/benchmark vectors and all four outcome horizons");
    }
}
