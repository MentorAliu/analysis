using System.Security.Cryptography;
using System.Text;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Analysis.Infrastructure.Persistence;

namespace Analysis.ScoringChecks;

// Artificial constant series and explicit perturbations for arithmetic/integrity checks only.
// Not a historical market episode; never referenced by either production host.
internal static class Synthetic
{
    public static readonly DateTimeOffset T = new(2021, 1, 8, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset K = T.AddDays(1);
    public static ScoringInput Input()
    {
        var facts = new List<ObservationFact>();
        foreach (var instrument in CatalogSeed.Instruments)
        {
            var observations = Series(instrument).ToArray(); var bytes = Bytes(observations);
            foreach (var o in observations) facts.Add(new(o, "fixture:" + instrument.Id, "synthetic-m3-v1",
                Convert.ToHexStringLower(SHA256.HashData(bytes)), K));
        }
        return new(T, K, CatalogSeed.Instruments.OrderBy(i => i.Id, StringComparer.Ordinal).ToArray(),
            facts.OrderBy(f => f.Observation.InstrumentId, StringComparer.Ordinal).ThenBy(f => f.Observation.Kind).ThenBy(f => f.Observation.EventTimeUtc).ToArray(), []);
    }
    public static IEnumerable<Observation> Series(InstrumentRef i)
    {
        for (var t = T.AddDays(-8); t <= T.AddHours(2); t = t.AddHours(1))
        {
            if (i.Kind == InstrumentKind.Spot)
                yield return new(i.Id, ObservationKind.Candle, t, 3600, i.BaseUnit, "USDT", Open: 100, High: 100, Low: 100, Close: 100, Volume: 10, QuoteVolume: 1000);
            else if (i.Kind == InstrumentKind.LinearPerpetual)
            {
                yield return new(i.Id, ObservationKind.OpenInterestBothSides, t, 3600, i.BaseUnit, null, Value: 1000);
                if (t.Hour % 8 == 0) yield return new(i.Id, ObservationKind.FundingRate, t, 0, "fraction", null, Value: 0);
            }
            else if (t.Hour == 0) yield return new(i.Id, ObservationKind.ChainTvl, t, 0, "USD", null, Value: 1_000_000);
        }
    }
    public static byte[] Bytes(Observation[] observations) => Encoding.UTF8.GetBytes(CanonicalJson.Write(new { fixture = "synthetic-m3-v1", observations }));
    public static ScoringInput Map(ScoringInput input, Func<ObservationFact, ObservationFact> transform) => input with { Observations = input.Observations.Select(transform).ToArray() };
    public static ScoringInput Remove(ScoringInput input, Func<ObservationFact, bool> remove) => input with { Observations = input.Observations.Where(f => !remove(f)).ToArray() };
    public static Observation Price(Observation o, decimal value) => o with { Open = value, High = value, Low = value, Close = value };
    public static FeatureSet Features(string asset = "ethereum") => new FeatureCalculator(ScoringModel.Slice1).Calculate(asset, Input());
    public static FeatureSet Values(FeatureSet features, params (int Id, decimal Value)[] changes) => features with
    { Values = features.Values.Select(f => changes.Any(c => c.Id == f.Id) ? f with { Value = changes.Single(c => c.Id == f.Id).Value } : f).ToArray() };
    public static FeatureSet Missing(FeatureSet features, params int[] ids) => features with
    { Values = features.Values.Select(f => ids.Contains(f.Id) ? f with { State = "missing", Reason = "synthetic-missing", Value = null } : f).ToArray() };
    public sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
}
