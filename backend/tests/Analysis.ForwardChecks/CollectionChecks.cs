using System.Collections.Concurrent;
using System.Text.Json;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Infrastructure.Adapters;
using Analysis.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Analysis.ForwardChecks;

internal static class CollectionChecks
{
    private sealed class Store : IForwardCollectionStore
    {
        public List<Observation> Saved { get; } = [];
        public List<string> Quarantined { get; } = [];
        public Task<ForwardCollectionWork[]> PlanInputsAsync(DateTimeOffset t, CancellationToken ct) => throw new NotSupportedException();
        public Task<ForwardCollectionWork[]> PlanOutcomesAsync(ReadWindow window, CancellationToken ct) => throw new NotSupportedException();
        public Task<ForwardStoreResult> SaveAsync(ForwardCollectionWork work, IReadOnlyList<ObservationPage> pages, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            var facts = pages.SelectMany(p => p.Observations).ToArray(); Saved.AddRange(facts);
            return Task.FromResult(new ForwardStoreResult(facts.Length, 0, 0));
        }
        public Task QuarantineAsync(ForwardCollectionWork work, string code, CancellationToken ct)
        { Quarantined.Add(code); return Task.CompletedTask; }
    }
    public static async Task RunAsync()
    {
        var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var app = builder.Build();
        var paths = new ConcurrentQueue<string>(); var mode = "scalar"; var time = Synthetic.T.ToUnixTimeMilliseconds();
        var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        app.Run(async context =>
        {
            paths.Enqueue(context.Request.Path + context.Request.QueryString);
            if (mode == "slow")
            {
                try { await Task.Delay(20_000, context.RequestAborted); }
                catch (OperationCanceledException) { cancelled.TrySetResult(); }
                return;
            }
            var symbol = context.Request.Query["symbol"].ToString();
            if (mode == "deny" && symbol == "BTCUSDT") { context.Response.StatusCode = 403; return; }
            object response = context.Request.Path.Value switch
            {
                "/v5/market/instruments-info" => new { retCode = 0, result = new { category = "linear", list = new[] {
                    new { symbol, baseCoin = symbol[..^4], quoteCoin = "USDT", settleCoin = "USDT", contractType = "LinearPerpetual", status = "Trading", fundingInterval = 480 } } } },
                "/v5/market/funding/history" => new { retCode = 0, result = new { category = "linear", list = new[] {
                    new { symbol, fundingRate = "-0.0001", fundingRateTimestamp = time.ToString() } } } },
                "/v5/market/open-interest" => new { retCode = 0, result = new { category = "linear", symbol, nextPageCursor = "", list = new[] {
                    new { openInterest = "12.12345678", timestamp = time.ToString() } } } },
                "/api/v3/exchangeInfo" => new { symbols = new[] { new { symbol, baseAsset = symbol[..^4], quoteAsset = "USDT", status = "TRADING", isSpotTradingAllowed = true } } },
                "/api/v3/klines" => new object[][] { [time - 3_600_000, "100", "100", "100", "100", "1", time - 1, "100", 1, "0", "0", "0"] },
                _ => throw new InvalidOperationException("Unexpected fixture request")
            };
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        });
        await app.StartAsync();
        try
        {
            var uri = new Uri(app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single());
            using var http = new OfflineHttp(uri); var store = new Store(); var jobs = new ForwardCollection(store);
            var perp = CatalogSeed.Instruments.Single(i => i.Id == "bybit:linear:BTCUSDT");
            var result = await jobs.CollectAsync([new(perp, new(Synthetic.T.AddHours(-120), Synthetic.T.AddMilliseconds(1)))],
                [new BybitDerivativesAdapter(http)], CancellationToken.None);
            Check.Equal("stored", result.Single().Status, "Actual Bybit adapter collection stores exact T");
            Check.That(store.Saved.Count == 2 && store.Saved.All(o => o.EventTimeUtc == Synthetic.T), "Funding and OI at T survive inclusive scalar window");
            Check.That(paths.Where(p => p.Contains("endTime=", StringComparison.Ordinal)).All(p => p.Contains($"endTime={time}", StringComparison.Ordinal)), "Inclusive provider end parameter equals T");
            Check.Equal(-0.0001m, store.Saved.Single(o => o.Kind == ObservationKind.FundingRate).Value, "Negative funding fraction preserved");
            paths.Clear(); store.Saved.Clear(); mode = "deny";
            var spots = CatalogSeed.Instruments.Where(i => i.Kind == InstrumentKind.Spot).Select(i => new ForwardCollectionWork(i, new(Synthetic.T.AddHours(-1), Synthetic.T))).ToArray();
            result = await jobs.CollectAsync(spots, [new BinanceMarketAdapter(http)], CancellationToken.None);
            Check.That(result.Count(r => r.Status == "quarantined") == 1 && result.Count(r => r.Status == "stored") == 2, "Denied instrument quarantined while unrelated instruments progress");
            Check.Equal(1, paths.Count(p => p.Contains("BTCUSDT", StringComparison.Ordinal)), "Permanent denial is not retried");
            Check.That(paths.All(p => p.StartsWith("/api/v3/", StringComparison.Ordinal)), "Outcome collection invokes Binance only");
            Check.That(store.Saved.All(o => o.EventTimeUtc.AddHours(1) <= Synthetic.T && o.QuoteUnit == "USDT"), "Outcome mapping accepts closed USDT candles");
            mode = "slow"; using var cancel = new CancellationTokenSource(100);
            await Check.ThrowsAsync<OperationCanceledException>(() => jobs.CollectAsync(spots, [new BinanceMarketAdapter(http)], cancel.Token), "Cancellation propagates through collection HTTP");
            await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));
            Check.Equal(1, store.Quarantined.Count, "Cancellation is not converted into provider quarantine");
            Check.Pass("Loopback forward collection: exact scalar T, closed candles, denial isolation and cancellation");
        }
        finally { await app.StopAsync(); }
    }
}
