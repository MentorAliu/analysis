using System.Security.Cryptography;
using System.Text;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Analysis.Domain.SignalsOutcomes;
using Analysis.Infrastructure.Persistence;

namespace Analysis.ForwardChecks;

internal static class Synthetic
{
    public static readonly DateTimeOffset T = new(2021, 1, 8, 0, 0, 0, TimeSpan.Zero);
    public static readonly DateTimeOffset K = T.AddMinutes(2);
    public static Observation Candle(InstrumentRef i, DateTimeOffset t, decimal price = 100) =>
        new(i.Id, ObservationKind.Candle, t, 3600, i.BaseUnit, "USDT", price, price, price, price, 10, 1000);
    public static byte[] Bytes(Observation[] observations) => Encoding.UTF8.GetBytes(CanonicalJson.Write(new { fixture = "synthetic-1a-v1", observations }));
    public static ObservationFact Fact(Observation o, DateTimeOffset ingested)
    {
        var bytes = Bytes([o]); var hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        return new(o, hash, "synthetic-1a-v1", hash, ingested);
    }
    public static Observation[] Series(InstrumentRef i, DateTimeOffset start, DateTimeOffset end) => Enumerable.Range(0, (int)Math.Ceiling((end - start).TotalHours))
        .SelectMany(n =>
        {
            var t = start.AddHours(n);
            if (i.Kind == InstrumentKind.Spot) return new[] { Candle(i, t) };
            if (i.Kind == InstrumentKind.Chain) return t.Hour == 0 ? new[] { new Observation(i.Id, ObservationKind.ChainTvl, t, 0, "USD", null, Value: 1000000) } : [];
            return t.Hour % 8 == 0 ? new[] { new Observation(i.Id, ObservationKind.OpenInterestBothSides, t, 3600, i.BaseUnit, null, Value: 1000),
                new Observation(i.Id, ObservationKind.FundingRate, t, 0, "fraction", null, Value: 0) } :
                [new Observation(i.Id, ObservationKind.OpenInterestBothSides, t, 3600, i.BaseUnit, null, Value: 1000)];
        }).Where(o => o.EventTimeUtc < end).ToArray();
    public static ScoringInput Input() => new(T, K, CatalogSeed.Instruments,
        CatalogSeed.Instruments.SelectMany(i => Series(i, T.AddHours(-120), i.Kind == InstrumentKind.Spot ? T : T.AddMilliseconds(1)))
            .Select(o => Fact(o, K)).ToArray(), []);
    public static ForwardOriginal Original(ScoringBundle? bundle = null)
    {
        bundle ??= ScoringJobs.Calculate(Input(), ScoringModel.Slice1);
        var model = ScoringModel.Slice1;
        return ForwardRecordingJobs.Original(new("fixture-batch", model.Manifest.ModelId, model.Hash, model.SourceHash, bundle, false),
            CatalogSeed.Assets, K.AddSeconds(1), K.AddSeconds(2));
    }
    public static OutcomeCapture Outcome(OutcomeTarget target, decimal terminal = 110)
    {
        var i = CatalogSeed.Instruments.Single(i => i.Id == target.InstrumentId);
        var facts = Enumerable.Range(0, target.HorizonHours + 1).Select(n => Fact(Candle(i,
            target.ReferenceBoundaryUtc.AddHours(n - 1), n == target.HorizonHours ? terminal : 100), target.MaturityUtc)).ToArray();
        return new(target.MaturityUtc, facts, []);
    }
}
