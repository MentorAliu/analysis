using System.Net;
using System.Text;
using Analysis.Application;
using Analysis.Infrastructure;
using Analysis.Infrastructure.Adapters;

namespace Analysis.CatalogChecks;

internal static class PrivateTransportChecks
{
    public static async Task RunAsync()
    {
        string[] valid = ["--ingest-once", "--private-use", "--country", "XK", "--start-utc", "2026-08-30T00:00:00Z", "--end-utc", "2026-09-02T00:00:00Z"];
        var now = DateTimeOffset.Parse("2026-09-03T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        Check.That(PrivateIngestionRequest.TryParse(valid, now, out var request) && request!.Window.EndUtc.Offset == TimeSpan.Zero, "Private command parses closed UTC window");
        foreach (var index in new[] { 1, 3, 5, 7 })
        {
            var invalid = valid.ToArray(); invalid[index] = "invalid";
            Check.That(!PrivateIngestionRequest.TryParse(invalid, now, out _), "Scope and timestamps fail closed");
        }
        foreach (var end in new[] { "2026-09-08T00:00:00Z", "2026-08-29T00:00:00Z", "2026-09-01T00:30:00Z", "2026-09-01T00:00:00+01:00" })
        {
            var invalid = valid.ToArray(); invalid[7] = end;
            Check.That(!PrivateIngestionRequest.TryParse(invalid, now, out _), "Future/large/reversed/unaligned/non-UTC windows refused");
        }
        Check.That(!PrivateIngestionRequest.TryParse(["--ingest-once"], now, out _), "No implicit live permission");
        Check.That(!PrivateIngestionRequest.TryParse([.. valid, "--anything"], now, out _), "Unknown flags rejected");
        Check.Throws<ArgumentException>(() => new PrivateProviderHttp("unreviewed"), "Provider allowlist");
        var sent = new List<string>();
        using (var http = new PrivateProviderHttp("binance", new Handler((message, _) =>
        {
            Check.That(message.Method == HttpMethod.Get && message.Headers.Authorization is null &&
                !message.Headers.Contains("Cookie") && message.Headers.UserAgent.ToString() == "AnalysisPrivateResearch/0.1", "Credential-free identified GET");
            sent.Add(message.RequestUri!.AbsoluteUri);
            return Task.FromResult(Json("[]"));
        }), 2, TimeSpan.Zero))
        {
            foreach (var path in new[] { "https://evil.invalid/", "//evil.invalid/", "/api/v3/account", "/api/v3/../account", "/api/v3/klines#fragment", "/api/v3/klines?bad=\\value" })
                await Check.ThrowsAsync<ArgumentException>(() => http.GetAsync(path, CancellationToken.None), "Unreviewed path refused before I/O");
            Check.That(sent.Count == 0, "Bad paths issue no requests");
            await http.GetAsync("/api/v3/klines?symbol=BTCUSDT", CancellationToken.None);
            await http.GetAsync("/api/v3/klines?symbol=ETHUSDT", CancellationToken.None);
            await Check.ThrowsAsync<ProviderReadException>(() => http.GetAsync("/api/v3/klines?symbol=SOLUSDT", CancellationToken.None), "Shared provider budget");
            Check.That(http.Attempts == 2 && sent.All(uri => uri.StartsWith("https://data-api.binance.vision/", StringComparison.Ordinal)), "Exact trusted origin and bounded attempts");
        }
        foreach (var status in new[] { HttpStatusCode.Forbidden, (HttpStatusCode)451, HttpStatusCode.TooManyRequests, HttpStatusCode.Redirect })
        {
            var calls = 0;
            using var http = new PrivateProviderHttp("bybit", new Handler((_, _) =>
            {
                calls++;
                var response = Json("[]"); response.StatusCode = status;
                response.Headers.RetryAfter = new(TimeSpan.FromSeconds(60));
                response.Headers.Location = new Uri("https://alternate.invalid/");
                return Task.FromResult(response);
            }), 128, TimeSpan.Zero);
            for (var asset = 0; asset < 3; asset++)
                await Check.ThrowsAsync<ProviderReadException>(() => http.GetAsync("/v5/market/instruments-info?symbol=BTCUSDT", CancellationToken.None), "Stop provider across instruments");
            Check.That(calls == 1, "No early retry, redirect or regional bypass");
        }
        var retries = 0;
        using (var http = new PrivateProviderHttp("bybit", new Handler((_, _) =>
        {
            retries++;
            var response = Json("{\"retCode\":10006}");
            response.Headers.Add("X-Bapi-Limit-Reset-Timestamp", "0");
            return Task.FromResult(response);
        }), 2, TimeSpan.Zero))
        {
            await Check.ThrowsAsync<ProviderReadException>(() => http.GetAsync("/v5/market/open-interest", CancellationToken.None), "Retries count toward run budget");
            Check.That(retries == 2, "Provider success-envelope retries bounded too");
        }
        using (var http = new PrivateProviderHttp("defillama", new Handler((_, _) => Task.FromResult(Json("[]"))), 128, TimeSpan.FromSeconds(1)))
        {
            await http.GetAsync("/v2/historicalChainTvl/Ethereum", CancellationToken.None);
            using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await Check.ThrowsAsync<OperationCanceledException>(() => http.GetAsync("/v2/historicalChainTvl/Solana", cancel.Token), "Cancellation while paced");
            Check.That(http.Attempts == 1, "Cancelled wait sends nothing");
        }
        Check.Pass("Private-use command gate, trusted destinations, no credentials, shared request/retry budgets, provider stop and paced cancellation");
    }

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
}
