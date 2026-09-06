using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Analysis.CatalogChecks;

internal sealed class FixtureServer : IAsyncDisposable
{
    private readonly WebApplication app;
    public Uri Address { get; }
    public ConcurrentQueue<string> Requests { get; } = new();
    public Func<HttpContext, Task>? Override { get; set; }
    public bool RepeatCursor { get; set; }
    public bool PaginateFunding { get; set; }
    public bool FailEther { get; set; }
    public bool Conflict { get; set; }
    public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int retryCount;
    public int RetryCount => retryCount;

    private FixtureServer(WebApplication application, Uri address) { app = application; Address = address; }

    public static async Task<FixtureServer> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        FixtureServer? server = null;
        app.Run(context => server!.Handle(context));
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        server = new(app, new Uri(address));
        return server;
    }

    private async Task Handle(HttpContext context)
    {
        Requests.Enqueue(context.Request.Path + context.Request.QueryString);
        if (Override is not null) { await Override(context); return; }
        var path = context.Request.Path.Value!;
        var symbol = context.Request.Query["symbol"].ToString();
        var baseUnit = symbol.EndsWith("USDT", StringComparison.Ordinal) ? symbol[..^4] : "INVALID";
        context.Response.ContentType = "application/json";
        if (path == "/retry")
        {
            var attempt = Interlocked.Increment(ref retryCount);
            if (attempt < 3) { context.Response.StatusCode = 429; context.Response.Headers.RetryAfter = "0"; }
            else await context.Response.WriteAsync("[]");
            return;
        }
        if (path == "/forbidden") { context.Response.StatusCode = 403; return; }
        if (path == "/redirect") { context.Response.StatusCode = 302; context.Response.Headers.Location = "https://example.invalid/"; return; }
        if (path == "/slow")
        {
            try { await Task.Delay(TimeSpan.FromSeconds(20), context.RequestAborted); }
            catch (OperationCanceledException) { CancellationObserved.TrySetResult(); }
            return;
        }
        if (FailEther && symbol == "ETHUSDT")
        {
            await context.Response.WriteAsync("{\"retCode\":0,\"result\":{\"category\":\"linear\",\"list\":[]}}");
            return;
        }
        object? response = path switch
        {
            "/api/v3/exchangeInfo" => new { symbols = new[] { new { symbol, baseAsset = baseUnit, quoteAsset = "USDT", status = "TRADING", isSpotTradingAllowed = true } } },
            "/v5/market/instruments-info" => new { retCode = 0, result = new { category = "linear", list = new[] { new { symbol, baseCoin = baseUnit, quoteCoin = "USDT", settleCoin = "USDT", contractType = "LinearPerpetual", status = "Trading", fundingInterval = 480 } } } },
            "/v5/market/funding/history" => Funding(symbol, context),
            "/v5/market/open-interest" => Interest(symbol, context),
            _ => null
        };
        if (response is not null) { await context.Response.WriteAsync(JsonSerializer.Serialize(response)); return; }
        if (path == "/api/v3/klines")
        {
            var candle = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "binance-hour-variant.json"));
            await context.Response.WriteAsync(Conflict ? candle.Replace("0.01577100", "0.01577200") : candle);
            return;
        }
        if (path.StartsWith("/v2/historicalChainTvl/", StringComparison.Ordinal))
        {
            await context.Response.WriteAsync(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "defillama-schema-example.json")));
            return;
        }
        context.Response.StatusCode = 404;
    }

    private object Funding(string symbol, HttpContext context)
    {
        var end = long.Parse(context.Request.Query["endTime"].ToString(), System.Globalization.CultureInfo.InvariantCulture);
        var list = PaginateFunding && end >= 1609462800000
            ? Enumerable.Range(0, 200).Select(i => new { symbol, fundingRate = "0.0001", fundingRateTimestamp = (1609462800199L - i).ToString(System.Globalization.CultureInfo.InvariantCulture) }).ToArray()
            : new[] { new { symbol, fundingRate = "0.0001", fundingRateTimestamp = "1609462800000" } };
        return new { retCode = 0, result = new { category = "linear", list } };
    }

    private object Interest(string symbol, HttpContext context)
    {
        var secondPage = context.Request.Query.ContainsKey("cursor");
        return new
        {
            retCode = 0,
            result = new
            {
                category = "linear", symbol,
                list = new[] { new { openInterest = "12.12345678", singleOpenInterest = "6.06172839", timestamp = secondPage ? "1609462800000" : "1609459200000", extraProviderField = "ignored" } },
                nextPageCursor = secondPage && !RepeatCursor ? "" : "opaque=cursor&next=1"
            }
        };
    }

    public async ValueTask DisposeAsync() { await app.StopAsync(); await app.DisposeAsync(); }
    public static byte[] Bytes(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));
    public static byte[] Json(string value) => Encoding.UTF8.GetBytes(value);
}
