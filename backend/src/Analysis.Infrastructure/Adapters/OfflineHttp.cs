using System.Net;
using System.Text.Json;
using Analysis.Application;

namespace Analysis.Infrastructure.Adapters;

// M2 is explicitly offline. This transport cannot target a live provider or follow redirects.
public sealed class OfflineHttp : IDisposable
{
    private readonly HttpClient client;

    public OfflineHttp(Uri fixtureServer)
    {
        if (!fixtureServer.IsLoopback || fixtureServer.Scheme != "http" ||
            fixtureServer.UserInfo.Length != 0 || fixtureServer.Query.Length != 0 || fixtureServer.AbsolutePath != "/")
            throw new ArgumentException("M2 permits only an HTTP loopback fixture server; live licensing/access is unresolved.");
        client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false, UseProxy = false, PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        }) { BaseAddress = fixtureServer, Timeout = TimeSpan.FromSeconds(15) };
    }

    public async Task<byte[]> GetAsync(string path, CancellationToken cancellationToken)
    {
        if (!path.StartsWith('/') || path.StartsWith("//") || path.Contains('\\'))
            throw new ArgumentException("Only relative API paths are permitted.");
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, path);
                request.Headers.Accept.ParseAdd("application/json");
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                if (response.StatusCode == HttpStatusCode.TooManyRequests || (int)response.StatusCode is 500 or 502 or 503 or 504)
                {
                    var delay = response.Headers.RetryAfter?.Delta ??
                        (response.Headers.RetryAfter?.Date - DateTimeOffset.UtcNow) ?? TimeSpan.FromMilliseconds(250 * (1 << attempt));
                    if (attempt == 2 || delay > TimeSpan.FromSeconds(10)) throw new ProviderReadException("retry-budget-exhausted");
                    await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, cancellationToken);
                    continue;
                }
                if (!response.IsSuccessStatusCode)
                    throw new ProviderReadException(response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "access-denied",
                        HttpStatusCode.NotFound => "coverage-unavailable",
                        _ => "http-failure"
                    });
                if (response.Content.Headers.ContentType?.MediaType != "application/json")
                    throw new ProviderReadException("unexpected-content-type");
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var data = new MemoryStream();
                var buffer = new byte[8192];
                // Content reads need their own budget with ResponseHeadersRead.
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(15));
                while (true)
                {
                    var count = await stream.ReadAsync(buffer, timeout.Token);
                    if (count == 0) break;
                    if (data.Length + count > 4 * 1024 * 1024) throw new ProviderReadException("payload-too-large");
                    data.Write(buffer, 0, count);
                }
                var bytes = data.ToArray();
                if (path.StartsWith("/v5/", StringComparison.Ordinal) && IsBybitRateLimited(bytes))
                {
                    var delay = TimeSpan.FromSeconds(1 << attempt);
                    if (response.Headers.TryGetValues("X-Bapi-Limit-Reset-Timestamp", out var values) &&
                        long.TryParse(values.FirstOrDefault(), out var reset))
                    {
                        if (reset is < 0 or > 253402300799999) throw new ProviderReadException("invalid-rate-limit-header");
                        delay = DateTimeOffset.FromUnixTimeMilliseconds(reset) - DateTimeOffset.UtcNow;
                    }
                    if (attempt == 2 || delay > TimeSpan.FromSeconds(10)) throw new ProviderReadException("provider-rate-limited");
                    await Task.Delay(delay > TimeSpan.Zero ? delay : TimeSpan.Zero, cancellationToken);
                    continue;
                }
                return bytes;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (attempt == 2) throw new ProviderReadException("http-timeout");
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt)), cancellationToken);
            }
            catch (Exception error) when (error is HttpRequestException or IOException)
            {
                if (attempt == 2) throw new ProviderReadException("network-failure");
                await Task.Delay(TimeSpan.FromMilliseconds(250 * (1 << attempt)), cancellationToken);
            }
        }
        throw new ProviderReadException("retry-budget-exhausted");
    }

    public void Dispose() => client.Dispose();

    private static bool IsBybitRateLimited(byte[] bytes)
    {
        try
        {
            using var json = JsonDocument.Parse(bytes);
            return json.RootElement.ValueKind == JsonValueKind.Object &&
                json.RootElement.TryGetProperty("retCode", out var code) && code.ValueKind == JsonValueKind.Number &&
                code.TryGetInt32(out var value) && value == 10006;
        }
        catch (JsonException) { return false; } // The adapter reports malformed payloads.
    }
}
