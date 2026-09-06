using System.Text.Json;
using Analysis.Application;
using Analysis.Domain;

namespace Analysis.Infrastructure.Adapters;

public sealed class DefiLlamaFundamentalsAdapter(IProviderHttp http) : IObservationAdapter
{
    public string ProviderId => "defillama";

    public async Task<IReadOnlyList<ObservationPage>> ReadAsync(InstrumentRef instrument, ReadWindow window, CancellationToken cancellationToken)
    {
        window.Validate();
        if (instrument.ProviderId != ProviderId || instrument.Kind != InstrumentKind.Chain ||
            (instrument.AssetId, instrument.NativeSymbol) is not (("ethereum", "Ethereum") or ("solana", "Solana")))
            throw new ProviderReadException("inapplicable-instrument");
        var path = $"/v2/historicalChainTvl/{Uri.EscapeDataString(instrument.NativeSymbol)}";
        var bytes = await http.GetAsync(path, cancellationToken);
        return [new(new(path, "defillama-chain-tvl-v1", bytes), Parse(bytes, instrument, window))];
    }

    public static IReadOnlyList<Observation> Parse(byte[] bytes, InstrumentRef instrument, ReadWindow window) => Mapping.Guard(() =>
    {
        using var json = JsonDocument.Parse(bytes);
        var observations = new List<Observation>();
        foreach (var item in json.RootElement.EnumerateArray())
        {
            var time = DateTimeOffset.FromUnixTimeSeconds(item.GetProperty("date").GetInt64());
            var observation = Mapping.Valid(new(instrument.Id, ObservationKind.ChainTvl, time, 0, "USD", null,
                Value: Mapping.DecimalNumber(item.GetProperty("tvl"))), instrument);
            if (Mapping.Within(time, window)) observations.Add(observation);
        }
        return (IReadOnlyList<Observation>)observations;
    });
}
