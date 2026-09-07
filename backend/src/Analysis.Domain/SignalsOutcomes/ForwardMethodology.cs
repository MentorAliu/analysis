using Analysis.Domain.Scoring;

namespace Analysis.Domain.SignalsOutcomes;

public sealed record ForwardManifest(string Id, string ModelId, string[] Universe, int[] HorizonHours,
    int PrimaryHorizonHours, int IssuanceGraceMinutes, int InputLookbackHours, int DecimalPlaces,
    string Midpoint, Dictionary<string, string> Policies);

public static class ForwardMethodology
{
    public static readonly string Json = ReadResource("Analysis.Domain.SignalsOutcomes.Manifests.forward-1a-v1.json");
    public static string Hash { get; } = CanonicalJson.Hash(Json);
    public static ForwardManifest Manifest => CanonicalJson.Read<ForwardManifest>(Json);
    public static string SourceHash { get; } = CanonicalJson.Hash(CanonicalJson.Write(typeof(ForwardMethodology).Assembly
        .GetManifestResourceNames().Where(n => n.EndsWith(".source", StringComparison.Ordinal)).Order(StringComparer.Ordinal)
        .Select(name => new { name, text = ReadResource(name, false) }).ToArray()));

    private static string ReadResource(string name, bool canonical = true)
    {
        using var reader = new StreamReader(typeof(ForwardMethodology).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Missing forward methodology resource."));
        var text = reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal);
        return canonical ? CanonicalJson.Normalize(text) : text;
    }
    public static DateTimeOffset Hour(DateTimeOffset utc)
    {
        Utc.Require(utc);
        return DateTimeOffset.FromUnixTimeMilliseconds(utc.ToUnixTimeMilliseconds() / 3_600_000 * 3_600_000);
    }
    public static void RequireIssuanceClock(DateTimeOffset t, DateTimeOffset k, DateTimeOffset created, DateTimeOffset issued)
    {
        foreach (var value in new[] { t, k, created, issued }) Utc.Require(value);
        if (t != Hour(t) || t != Hour(issued) || k < t || created < k || issued < created ||
            issued > t.AddMinutes(Manifest.IssuanceGraceMinutes)) throw new ArgumentException("invalid-forward-clock");
    }
    public static decimal Round(decimal value) => decimal.Round(value, Manifest.DecimalPlaces, MidpointRounding.ToEven);
    public static decimal RatioReturn(decimal terminal, decimal reference)
    {
        if (reference <= 0 || terminal <= 0) throw new ArgumentException("invalid-price");
        var value = Round(checked(Round(checked(terminal / reference)) - 1m));
        ExactDecimal.Require(value);
        return value;
    }
    public static OutcomeTarget[] Targets(ForwardOriginal original) => original.UniverseAssetIds.SelectMany(asset =>
        Manifest.HorizonHours.Select(h => new OutcomeTarget(original.Id, asset,
            original.Instruments.Single(i => i.AssetId == asset && i.Kind == InstrumentKind.Spot).Id,
            h, original.ReferenceBoundaryUtc, original.ReferenceBoundaryUtc.AddHours(h)))).ToArray();
}
