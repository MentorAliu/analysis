using System.Text.Json;
using Analysis.Application;
using Analysis.CatalogChecks;
using Analysis.Domain;
using Analysis.Infrastructure.Adapters;
using Analysis.Infrastructure.Persistence;

try
{
    if (args is ["--private-snapshot", var from, var to])
    {
        string[] command = ["--ingest-once", "--private-use", "--country", "XK", "--start-utc", from, "--end-utc", to];
        if (!Analysis.Infrastructure.PrivateIngestionRequest.TryParse(command, DateTimeOffset.UtcNow, out var request))
            throw new ArgumentException("Invalid private snapshot window");
        Console.WriteLine(JsonSerializer.Serialize(await DatabaseChecks.PrivateSnapshotAsync(request!.Window)));
        return 0;
    }
    if (args is ["--verify-persistence"])
    {
        Console.WriteLine(JsonSerializer.Serialize(new { databaseSnapshot = await DatabaseChecks.PersistenceSnapshotAsync() }));
        return 0;
    }
    await PrivateTransportChecks.RunAsync();
    var start = DateTimeOffset.FromUnixTimeSeconds(1609459200);
    var window = new ReadWindow(start, start.AddDays(1));
    var spot = CatalogSeed.Instruments.First(i => i.ProviderId == "binance");
    var perp = CatalogSeed.Instruments.First(i => i.ProviderId == "bybit");
    var ethPerp = CatalogSeed.Instruments.Single(i => i.ProviderId == "bybit" && i.AssetId == "ethereum");
    var chain = CatalogSeed.Instruments.First(i => i.ProviderId == "defillama");

    Check.That(CatalogSeed.Assets.Length == 3 && CatalogSeed.Instruments.Length == 8, "Catalog shape");
    Check.That(!CatalogSeed.Instruments.Any(i => i.AssetId == "bitcoin" && i.Kind == InstrumentKind.Chain), "BTC TVL must be inapplicable");
    Check.That(spot.Id != perp.Id && spot.AssetId == perp.AssetId, "Spot and perp identities differ");
    Check.Pass("Canonical identities and BTC fundamentals inapplicability");

    foreach (var value in new[] { "0.000000000000000001", "12345678901234567890.12345678", "-0.0001", "0", "12.00000000000000000000000" })
        Check.That(ExactDecimal.Parse(value) == decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture), "Exact decimal");
    foreach (var value in new[] { "0.0000000000000000001", "12345678901234567890.123456789", "NaN", "1e-8", "1,25", "", " 1", "+1" })
        Check.Throws<FormatException>(() => ExactDecimal.Parse(value), value);
    Check.Throws<ArgumentException>(() => new ReadWindow(start.ToOffset(TimeSpan.FromHours(1)), start.AddHours(2)).Validate(), "non UTC");
    Check.Throws<ArgumentException>(() => new ReadWindow(start.AddTicks(1), start.AddHours(2)).Validate(), "sub-ms timestamp");
    Check.Throws<ArgumentException>(() => new ReadWindow(start, start.AddDays(31)).Validate(), "read bound");
    Check.Pass("Decimal precision rejects rounding, explicit UTC and bounded windows");

    Check.Throws<ProviderReadException>(() => BinanceMarketAdapter.ParseCandles(FixtureServer.Bytes("binance-documented.json"), spot, window), "documented non-hour candle");
    var mapped = BinanceMarketAdapter.ParseCandles(FixtureServer.Bytes("binance-hour-variant.json"), spot, window).Observations.Single();
    Check.That(mapped.Close == 0.01577100m && mapped.Volume == 148976.11427815m && mapped.QuoteVolume == 2434.19055334m && mapped.Unit == "BTC" && mapped.QuoteUnit == "USDT", "Candle units/precision");
    Check.That(BinanceMarketAdapter.ParseCandles(FixtureServer.Bytes("binance-hour-variant.json"), spot, new(start, start.AddMinutes(30))).Observations.Count == 0, "Exclude incomplete candle");
    Check.That(BinanceMarketAdapter.ParseCandles(FixtureServer.Bytes("binance-hour-variant.json"), spot, new(start.AddHours(1), start.AddHours(2))).Observations.Count == 0, "No gap interpolation");
    Check.Throws<ProviderReadException>(() => BinanceMarketAdapter.ParseCandles(FixtureServer.Json("[[1,2]]"), spot, window), "missing fields");
    Check.Throws<ProviderReadException>(() => BinanceMarketAdapter.ParseCandles(FixtureServer.Json(System.Text.Encoding.UTF8.GetString(FixtureServer.Bytes("binance-hour-variant.json")).Replace("0.01577100", "NaN")), spot, window), "bad decimal");
    Check.Pass("Documented candles, closed-bar boundaries, units, gaps and missing fields");

    Check.Throws<ProviderReadException>(() => BybitDerivativesAdapter.Parse(FixtureServer.Bytes("bybit-funding-documented.json"), ethPerp, window, ObservationKind.FundingRate), "ETHPERP is not ETHUSDT");
    Check.Throws<ProviderReadException>(() => BybitDerivativesAdapter.Parse(FixtureServer.Bytes("bybit-oi-documented.json"), perp, window, ObservationKind.OpenInterestBothSides), "inverse is not linear");
    var tvl = DefiLlamaFundamentalsAdapter.Parse(FixtureServer.Bytes("defillama-schema-example.json"), chain, window).Single();
    Check.That(tvl.Value == 45000000000m && tvl.Unit == "USD" && tvl.EventTimeUtc == start && tvl.PeriodSeconds == 0, "TVL has USD and provider timestamp");
    Check.Pass("Official derivatives examples reject wrong contracts; chain TVL schema maps exact USD/seconds");

    var typeChangedCandle = System.Text.Encoding.UTF8.GetString(FixtureServer.Bytes("binance-hour-variant.json")).Replace("\"0.01577100\"", "0.01577100");
    Check.Throws<ProviderReadException>(() => BinanceMarketAdapter.ParseCandles(FixtureServer.Json(typeChangedCandle), spot, window), "String-to-number schema drift");
    Check.Throws<ProviderReadException>(() => DefiLlamaFundamentalsAdapter.Parse(FixtureServer.Json("[{\"date\":1609459200,\"tvl\":\"45000000000\"}]"), chain, window), "Number-to-string schema drift");
    Check.Throws<ProviderReadException>(() => BybitDerivativesAdapter.Parse(FixtureServer.Json("{\"retCode\":0,\"result\":{\"category\":\"linear\",\"symbol\":\"BTCUSDT\",\"list\":[]}}"), perp, window, ObservationKind.OpenInterestBothSides), "Missing required cursor");
    Check.Pass("Documented numeric field types and required pagination fields fail closed on drift");

    Check.Throws<ArgumentException>(() => new OfflineHttp(new Uri("https://api.bybit.com")), "Live host denied");
    await using var server = await FixtureServer.StartAsync();
    using var http = new OfflineHttp(server.Address);
    IObservationAdapter[] adapters = [new BinanceMarketAdapter(http), new BybitDerivativesAdapter(http), new DefiLlamaFundamentalsAdapter(http)];
    foreach (var instrument in CatalogSeed.Instruments)
    {
        var pages = await adapters.Single(a => a.ProviderId == instrument.ProviderId).ReadAsync(instrument, window, CancellationToken.None);
        Check.That(pages.SelectMany(p => p.Observations).All(o => o.InstrumentId == instrument.Id), "Context identity preserved");
        if (instrument.Kind == InstrumentKind.LinearPerpetual)
        {
            Check.That(pages.SelectMany(p => p.Observations).Single(o => o.Kind == ObservationKind.FundingRate).Value == 0.0001m, "Funding fraction is not percent");
            Check.That(pages.SelectMany(p => p.Observations).Where(o => o.Kind == ObservationKind.OpenInterestBothSides).All(o => o.Unit == instrument.BaseUnit && o.Value == 12.12345678m), "Both sides OI is not halved or converted to USD");
        }
    }
    Check.That(server.Requests.Any(p => p.Contains("cursor=opaque%3Dcursor%26next%3D1", StringComparison.Ordinal)), "Cursor encoded");
    Check.Pass("All eight synthetic contract variants via loopback HTTP, metadata checks and OI cursor pagination");

    server.PaginateFunding = true;
    await adapters[1].ReadAsync(perp, window, CancellationToken.None);
    Check.That(server.Requests.Any(p => p.Contains("endTime=1609462799999", StringComparison.Ordinal)), "Funding advances backward");
    server.PaginateFunding = false;
    server.RepeatCursor = true;
    await Check.ThrowsAsync<ProviderReadException>(() => adapters[1].ReadAsync(perp, window, CancellationToken.None), "stuck cursor");
    server.RepeatCursor = false;
    await http.GetAsync("/retry", CancellationToken.None);
    Check.That(server.RetryCount == 3, "Bounded retries");
    await Check.ThrowsAsync<ProviderReadException>(() => http.GetAsync("/forbidden", CancellationToken.None), "403 not retried");
    Check.That(server.Requests.Count(x => x == "/forbidden") == 1, "Permanent failure retries");
    await Check.ThrowsAsync<ProviderReadException>(() => http.GetAsync("/redirect", CancellationToken.None), "Redirect never followed");
    using (var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
        await Check.ThrowsAsync<OperationCanceledException>(() => http.GetAsync("/slow", cancel.Token), "In-flight HTTP cancellation");
    await server.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(3));
    Check.Pass("Funding pagination, cursor guard, Retry-After, permanent failures, redirect guard and real HTTP cancellation");

    var calls = 0;
    server.Override = async context =>
    {
        context.Response.ContentType = "application/json";
        context.Response.Headers["X-Bapi-Limit-Reset-Timestamp"] = "1609459200000";
        await context.Response.WriteAsync(++calls < 3 ? "{\"retCode\":10006}" : "{\"retCode\":0}");
    };
    await http.GetAsync("/v5/rate-limit-check", CancellationToken.None);
    Check.That(calls == 3, "Bybit 200/10006 is retried with reset header");
    calls = 0;
    server.Override = context =>
    {
        calls++;
        context.Response.StatusCode = 429;
        context.Response.Headers.RetryAfter = "60";
        return Task.CompletedTask;
    };
    await Check.ThrowsAsync<ProviderReadException>(() => http.GetAsync("/budget", CancellationToken.None), "Long Retry-After stops without an early retry");
    Check.That(calls == 1, "Respect long Retry-After");
    server.Override = async context =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"retCode\":\"wrong-type\"}");
    };
    await Check.ThrowsAsync<ProviderReadException>(() => adapters[1].ReadAsync(perp, window, CancellationToken.None), "Wrong retCode type fails as structured schema error");
    server.Override = async context =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(new string('x', 4 * 1024 * 1024 + 1));
    };
    await Check.ThrowsAsync<ProviderReadException>(() => http.GetAsync("/oversized", CancellationToken.None), "Bounded payload");
    server.Override = null;
    Check.Pass("Provider-level rate limits, long Retry-After, malformed envelope and bounded payloads");

    if (args is ["--database"])
        await DatabaseChecks.RunAsync(server, adapters, window);
    else if (args.Length != 0) throw new ArgumentException("Supported option: --database");
    Console.WriteLine(JsonSerializer.Serialize(new { passed = Check.Passed, failed = Array.Empty<string>(), database = args.Length > 0 ? "passed" : "not-requested", databaseSnapshot = DatabaseChecks.Snapshot, mode = "offline" }));
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine($"FAIL {error.GetType().Name}: {error.Message}");
    return 1;
}
