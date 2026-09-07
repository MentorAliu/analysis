using Analysis.Domain.Scoring;

namespace Analysis.Domain.SignalsOutcomes;

public static class BenchmarkCalculator
{
    public static RelativeStrengthOrdering Order(ScoringBundle bundle, ForwardItem[] items)
    {
        var eligible = items.Where(i => i.Rank.HasValue).OrderBy(i => i.Asset.Id, StringComparer.Ordinal).ToArray();
        if (eligible.Length == 0) return new("unavailable", "no-eligible-assets", []);
        var btc = bundle.Assets.Single(a => a.Features.AssetId == "bitcoin").Features.Values.Single(f => f.Id == 5);
        var btcSnapshot = items.Single(i => i.Asset.Id == "bitcoin").FeatureSnapshotId;
        if (btc.State != "available" || btc.Value is null || btc.Value <= -1) return new("unavailable", "missing-bitcoin-return", []);
        var values = new List<RelativeStrengthItem>();
        try
        {
            foreach (var item in eligible)
            {
                var value = bundle.Assets.Single(a => a.Features.AssetId == item.Asset.Id).Features.Values.Single(f => f.Id == 5);
                if (value.State != "available" || value.Value is null || value.Value <= -1) return new("unavailable", "missing-asset-return", []);
                values.Add(new(item.Asset.Id, 0, ForwardMethodology.RatioReturn(checked(1 + value.Value.Value), checked(1 + btc.Value.Value)),
                    value.Value.Value, btc.Value.Value, item.FeatureSnapshotId, btcSnapshot));
            }
        }
        catch (Exception error) when (error is OverflowException or FormatException) { return new("unavailable", "numeric-range", []); }
        return new("available", "frozen-feature-5", values.OrderByDescending(v => v.Value).ThenBy(v => v.AssetId, StringComparer.Ordinal)
            .Select((v, index) => v with { Rank = index + 1 }).ToArray());
    }

    public static ForwardAggregate[] Aggregate(IEnumerable<ForwardInspection> inspections)
    {
        var expanded = inspections.SelectMany(i => ForwardMethodology.Manifest.HorizonHours.Select(h => new { Inspection = i, H = h }));
        return expanded.GroupBy(x => new { x.Inspection.Original.ModelId, x.Inspection.Original.ManifestHash,
            x.Inspection.Original.MethodologyId, x.Inspection.Original.MethodologyHash, Horizon = x.H,
            Eligible = string.Join(",", x.Inspection.Original.EligibleAssetIds.Order(StringComparer.Ordinal)),
            Quality = string.Join(",", x.Inspection.Original.Items.OrderBy(i => i.Asset.Id, StringComparer.Ordinal).Select(i => $"{i.Asset.Id}:{i.Score.State}")) })
            .OrderBy(g => CanonicalJson.Write(g.Key), StringComparer.Ordinal).Select(group =>
            {
                var exclusions = new Dictionary<string, int>(StringComparer.Ordinal);
                var states = new[] { "complete", "conflicted", "incomplete", "pending", "unassessed" }.ToDictionary(s => s, _ => 0);
                var issues = new Dictionary<string, string>(StringComparer.Ordinal);
                var byRank = new Dictionary<int, List<decimal>> { [1] = [], [2] = [], [3] = [] };
                var paired = new List<(decimal Model, decimal Btc, decimal Rs)>(); var eligible = 0;
                foreach (var entry in group.OrderBy(e => e.Inspection.Original.IssuedAtUtc).ThenBy(e => e.Inspection.Original.Id, StringComparer.Ordinal))
                {
                    var issue = entry.Inspection.Original;
                    var outcomes = entry.Inspection.Outcomes.Where(o => o.Result.Target.HorizonHours == entry.H).ToDictionary(o => o.Result.Target.AssetId);
                    foreach (var asset in issue.UniverseAssetIds) states[outcomes.TryGetValue(asset, out var state) ? state.Result.State : "unassessed"]++;
                    foreach (var item in issue.Items.Where(i => i.Rank.HasValue))
                        if (outcomes.TryGetValue(item.Asset.Id, out var o) && o.Result.State == "complete") byRank[item.Rank!.Value].Add(o.Result.Return!.Value);
                    if (issue.EligibleAssetIds.Length > 0) eligible++;
                    var reason = issue.EligibleAssetIds.Length == 0 ? "no-eligible-assets" :
                        issue.RelativeStrength.State != "available" ? "benchmark-unavailable" :
                        outcomes.Values.Any(o => o.Result.State == "conflicted") ? "conflicted" :
                        outcomes.Values.Any(o => o.Result.State == "incomplete") ? "incomplete" :
                        outcomes.Count != 3 || outcomes.Values.Any(o => o.Result.State == "pending") ? "pending-or-unassessed" : null;
                    if (reason is not null) { exclusions[reason] = exclusions.GetValueOrDefault(reason) + 1; continue; }
                    var modelAsset = issue.Items.Single(i => i.Rank == 1).Asset.Id;
                    var rsAsset = issue.RelativeStrength.Items.Single(i => i.Rank == 1).AssetId;
                    paired.Add((outcomes[modelAsset].Result.Return!.Value, outcomes["bitcoin"].Result.Return!.Value, outcomes[rsAsset].Result.Return!.Value));
                }
                var key = group.Key;
                return new ForwardAggregate(key.ModelId, key.ManifestHash, key.MethodologyId, key.MethodologyHash, key.Eligible, key.Quality,
                    key.Horizon, group.Count(), eligible, paired.Count, exclusions, byRank.ToDictionary(p => p.Key, p => p.Value.Count), states, issues,
                    byRank.ToDictionary(p => p.Key, p => Mean(p.Value, "rank-" + p.Key, issues)),
                    Mean(paired.Select(p => p.Model), "model", issues), Mean(paired.Select(p => p.Btc), "bitcoin", issues), Mean(paired.Select(p => p.Rs), "relative-strength", issues),
                    Mean(paired.Select(p => checked(p.Model - p.Btc)), "model-minus-bitcoin", issues), Mean(paired.Select(p => checked(p.Model - p.Rs)), "model-minus-relative-strength", issues));
            }).ToArray();
    }
    // Exact integer reduction prevents sum overflow and cumulative rounding error.
    private static decimal? Mean(IEnumerable<decimal> sequence, string metric, Dictionary<string, string> issues)
    {
        var values = sequence.ToArray();
        if (values.Length == 0) return null;
        // Exact scaled integer reduction avoids repeated division/rounding and sum overflow.
        var total = System.Numerics.BigInteger.Zero;
        foreach (var value in values)
        {
            var bits = decimal.GetBits(value);
            var coefficient = (System.Numerics.BigInteger)(uint)bits[0] + ((System.Numerics.BigInteger)(uint)bits[1] << 32) + ((System.Numerics.BigInteger)(uint)bits[2] << 64);
            if (bits[3] < 0) coefficient = -coefficient;
            var scale = (bits[3] >> 16) & 255;
            total += coefficient * System.Numerics.BigInteger.Pow(10, 18 - scale);
        }
        var absolute = System.Numerics.BigInteger.Abs(total);
        var rounded = System.Numerics.BigInteger.DivRem(absolute, values.Length, out var remainder);
        if (2 * remainder > values.Length || 2 * remainder == values.Length && !rounded.IsEven) rounded++;
        var digits = rounded.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(19, '0');
        try { return ExactDecimal.Parse((total.Sign < 0 ? "-" : "") + digits[..^18] + "." + digits[^18..]); }
        catch (FormatException) { issues.Add(metric, "numeric-range"); return null; }
    }
}
