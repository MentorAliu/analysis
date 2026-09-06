using System.Text.Json;
using Analysis.Application;
using Analysis.Domain;

namespace Analysis.Infrastructure.Adapters;

public sealed class BinanceMarketAdapter(OfflineHttp http) : IObservationAdapter
{
    public string ProviderId => "binance";

    public async Task<IReadOnlyList<ObservationPage>> ReadAsync(InstrumentRef instrument, ReadWindow window, CancellationToken cancellationToken)
    {
        window.Validate();
        if (instrument.ProviderId != ProviderId || instrument.Kind != InstrumentKind.Spot)
            throw new ProviderReadException("instrument-mismatch");
        var symbol = Uri.EscapeDataString(instrument.NativeSymbol);
        var metadataPath = $"/api/v3/exchangeInfo?symbol={symbol}";
        var metadata = await http.GetAsync(metadataPath, cancellationToken);
        ValidateInstrument(metadata, instrument);
        var pages = new List<ObservationPage> { new(new(metadataPath, "binance-spot-v1", metadata), []) };
        var start = window.StartUtc.ToUnixTimeMilliseconds();
        var end = window.EndUtc.ToUnixTimeMilliseconds();
        for (var page = 0; page < 32; page++)
        {
            var path = $"/api/v3/klines?symbol={symbol}&interval=1h&timeZone=0&startTime={start}&endTime={end - 1}&limit=1000";
            var bytes = await http.GetAsync(path, cancellationToken);
            var mapped = ParseCandles(bytes, instrument, window);
            pages.Add(new(new(path, "binance-spot-v1", bytes), mapped.Observations));
            if (mapped.Count < 1000 || mapped.LastOpen + 3_600_000 >= end) return pages;
            if (mapped.LastOpen < start) throw new ProviderReadException("pagination-not-advancing");
            start = checked(mapped.LastOpen + 3_600_000);
        }
        throw new ProviderReadException("pagination-budget-exhausted");
    }

    public static void ValidateInstrument(byte[] bytes, InstrumentRef instrument) => Mapping.Guard(() =>
    {
        using var json = JsonDocument.Parse(bytes);
        var entries = json.RootElement.GetProperty("symbols");
        if (entries.GetArrayLength() != 1) throw new ProviderReadException("instrument-mismatch");
        var item = entries[0];
        Mapping.Equal(item.GetProperty("symbol").GetString(), instrument.NativeSymbol);
        Mapping.Equal(item.GetProperty("baseAsset").GetString(), instrument.BaseUnit);
        Mapping.Equal(item.GetProperty("quoteAsset").GetString(), instrument.QuoteUnit!);
        Mapping.Equal(item.GetProperty("status").GetString(), "TRADING");
        if (!item.GetProperty("isSpotTradingAllowed").GetBoolean()) throw new ProviderReadException("coverage-unavailable");
        return true;
    });

    public static (IReadOnlyList<Observation> Observations, int Count, long LastOpen) ParseCandles(
        byte[] bytes, InstrumentRef instrument, ReadWindow window) => Mapping.Guard(() =>
    {
        using var json = JsonDocument.Parse(bytes);
        var rows = json.RootElement;
        var observations = new List<Observation>();
        long previous = -1;
        foreach (var row in rows.EnumerateArray())
        {
            if (row.GetArrayLength() < 12) throw new ProviderReadException("schema-or-unit-mismatch");
            var open = row[0].GetInt64();
            if (open < previous) throw new ProviderReadException("unordered-candles");
            if (row[6].GetInt64() != checked(open + 3_600_000 - 1))
                throw new ProviderReadException("candle-interval-mismatch");
            previous = open;
            var time = DateTimeOffset.FromUnixTimeMilliseconds(open);
            var observation = Mapping.Valid(new(instrument.Id, ObservationKind.Candle, time, 3600,
                instrument.BaseUnit, instrument.QuoteUnit, Mapping.DecimalText(row[1]), Mapping.DecimalText(row[2]),
                Mapping.DecimalText(row[3]), Mapping.DecimalText(row[4]), Mapping.DecimalText(row[5]), Mapping.DecimalText(row[7])), instrument);
            if (Mapping.Within(time, window) && time.AddHours(1) <= window.EndUtc) observations.Add(observation);
        }
        return ((IReadOnlyList<Observation>)observations, rows.GetArrayLength(), previous);
    });
}
