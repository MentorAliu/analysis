using System.Text.Json;
using Analysis.Application;
using Analysis.Domain;

namespace Analysis.Infrastructure.Adapters;

public sealed class BybitDerivativesAdapter(OfflineHttp http) : IObservationAdapter
{
    public string ProviderId => "bybit";

    public async Task<IReadOnlyList<ObservationPage>> ReadAsync(InstrumentRef instrument, ReadWindow window, CancellationToken cancellationToken)
    {
        window.Validate();
        if (instrument.ProviderId != ProviderId || instrument.Kind != InstrumentKind.LinearPerpetual)
            throw new ProviderReadException("instrument-mismatch");
        var symbol = Uri.EscapeDataString(instrument.NativeSymbol);
        var metadataPath = $"/v5/market/instruments-info?category=linear&symbol={symbol}";
        var metadata = await http.GetAsync(metadataPath, cancellationToken);
        ValidateInstrument(metadata, instrument);
        var pages = new List<ObservationPage> { new(new(metadataPath, "bybit-linear-v1", metadata), []) };
        var start = window.StartUtc.ToUnixTimeMilliseconds();
        var end = window.EndUtc.ToUnixTimeMilliseconds() - 1;
        for (var page = 0; ; page++)
        {
            if (page == 32) throw new ProviderReadException("pagination-budget-exhausted");
            var path = $"/v5/market/funding/history?category=linear&symbol={symbol}&startTime={start}&endTime={end}&limit=200";
            var bytes = await http.GetAsync(path, cancellationToken);
            var mapped = Parse(bytes, instrument, window, ObservationKind.FundingRate);
            pages.Add(new(new(path, "bybit-linear-v1", bytes), mapped.Observations));
            if (mapped.Count < 200 || mapped.Oldest <= start) break;
            if (mapped.Oldest > end) throw new ProviderReadException("pagination-not-advancing");
            end = mapped.Oldest - 1;
        }
        var cursor = "";
        var seen = new HashSet<string>();
        for (var page = 0; page < 32; page++)
        {
            var path = $"/v5/market/open-interest?category=linear&symbol={symbol}&intervalTime=1h&startTime={start}&endTime={window.EndUtc.ToUnixTimeMilliseconds() - 1}&limit=200" +
                (cursor.Length == 0 ? "" : $"&cursor={Uri.EscapeDataString(cursor)}");
            var bytes = await http.GetAsync(path, cancellationToken);
            var mapped = Parse(bytes, instrument, window, ObservationKind.OpenInterestBothSides);
            pages.Add(new(new(path, "bybit-linear-v1", bytes), mapped.Observations));
            if (mapped.Cursor.Length == 0) return pages;
            if (!seen.Add(mapped.Cursor) || mapped.Cursor.Length > 1024) throw new ProviderReadException("pagination-not-advancing");
            cursor = mapped.Cursor;
        }
        throw new ProviderReadException("pagination-budget-exhausted");
    }

    private static JsonElement Result(JsonElement root)
    {
        var code = root.GetProperty("retCode").GetInt32();
        if (code != 0) throw new ProviderReadException(code == 10006 ? "provider-rate-limited" : "provider-error");
        var result = root.GetProperty("result");
        Mapping.Equal(result.GetProperty("category").GetString(), "linear");
        return result;
    }

    public static void ValidateInstrument(byte[] bytes, InstrumentRef instrument) => Mapping.Guard(() =>
    {
        using var json = JsonDocument.Parse(bytes);
        var result = Result(json.RootElement);
        var list = result.GetProperty("list");
        if (list.GetArrayLength() != 1) throw new ProviderReadException("instrument-mismatch");
        var item = list[0];
        Mapping.Equal(item.GetProperty("symbol").GetString(), instrument.NativeSymbol);
        Mapping.Equal(item.GetProperty("baseCoin").GetString(), instrument.BaseUnit);
        Mapping.Equal(item.GetProperty("quoteCoin").GetString(), instrument.QuoteUnit!);
        Mapping.Equal(item.GetProperty("settleCoin").GetString(), instrument.SettlementUnit!);
        Mapping.Equal(item.GetProperty("contractType").GetString(), "LinearPerpetual");
        Mapping.Equal(item.GetProperty("status").GetString(), "Trading");
        if (item.GetProperty("fundingInterval").GetInt32() <= 0) throw new ProviderReadException("invalid-funding-interval");
        return true;
    });

    public static (IReadOnlyList<Observation> Observations, int Count, long Oldest, string Cursor) Parse(
        byte[] bytes, InstrumentRef instrument, ReadWindow window, ObservationKind kind) => Mapping.Guard(() =>
    {
        if (kind is not (ObservationKind.FundingRate or ObservationKind.OpenInterestBothSides)) throw new ArgumentException("Invalid kind.");
        using var json = JsonDocument.Parse(bytes);
        var result = Result(json.RootElement);
        if (kind == ObservationKind.OpenInterestBothSides) Mapping.Equal(result.GetProperty("symbol").GetString(), instrument.NativeSymbol);
        var list = result.GetProperty("list");
        var observations = new List<Observation>();
        var oldest = long.MaxValue;
        foreach (var item in list.EnumerateArray())
        {
            if (kind == ObservationKind.FundingRate) Mapping.Equal(item.GetProperty("symbol").GetString(), instrument.NativeSymbol);
            var time = Mapping.IntegerText(item.GetProperty(kind == ObservationKind.FundingRate ? "fundingRateTimestamp" : "timestamp"));
            oldest = Math.Min(oldest, time);
            var observation = Mapping.Valid(new(instrument.Id, kind, DateTimeOffset.FromUnixTimeMilliseconds(time),
                kind == ObservationKind.FundingRate ? 0 : 3600, kind == ObservationKind.FundingRate ? "fraction" : instrument.BaseUnit,
                null, Value: Mapping.DecimalText(item.GetProperty(kind == ObservationKind.FundingRate ? "fundingRate" : "openInterest"))), instrument);
            if (Mapping.Within(observation.EventTimeUtc, window)) observations.Add(observation);
        }
        var cursor = kind == ObservationKind.OpenInterestBothSides
            ? result.GetProperty("nextPageCursor").GetString() ?? throw new ProviderReadException("missing-pagination-cursor") : "";
        return ((IReadOnlyList<Observation>)observations, list.GetArrayLength(), oldest, cursor);
    });
}
