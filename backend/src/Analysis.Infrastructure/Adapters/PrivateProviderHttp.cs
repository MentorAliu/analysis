using System.Diagnostics;
using Analysis.Application;

namespace Analysis.Infrastructure.Adapters;

// Explicit personal-use path reviewed in the active M2 plan, 2026-09-06.
// No caller-supplied host, account endpoints, authentication, redirects or proxy.
public sealed class PrivateProviderHttp : IProviderHttp, IDisposable
{
    private readonly string providerId;
    private readonly JsonHttp http;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly int requestLimit;
    private readonly TimeSpan spacing;
    private long? lastAttempt;
    private string? stoppedCode;
    public int Attempts { get; private set; }

    public PrivateProviderHttp(string providerId) : this(providerId, null, 128, TimeSpan.FromSeconds(1)) { }

    // Only the friend test assembly supplies an in-memory handler/smaller budget.
    internal PrivateProviderHttp(string providerId, HttpMessageHandler? handler, int requestLimit, TimeSpan spacing)
    {
        this.providerId = providerId;
        this.requestLimit = requestLimit;
        this.spacing = spacing;
        var origin = providerId switch
        {
            "binance" => "https://data-api.binance.vision/",
            "bybit" => "https://api.bybit.com/",
            "defillama" => "https://api.llama.fi/",
            _ => throw new ArgumentException("Provider is outside the reviewed private-use scope.")
        };
        http = new(new Uri(origin), handler, BeforeAttemptAsync);
    }

    public async Task<byte[]> GetAsync(string path, CancellationToken cancellationToken)
    {
        var endpoint = path.Split('?', 2)[0];
        var allowed = providerId switch
        {
            "binance" => endpoint is "/api/v3/exchangeInfo" or "/api/v3/klines",
            "bybit" => endpoint is "/v5/market/instruments-info" or "/v5/market/funding/history" or "/v5/market/open-interest",
            "defillama" => path is "/v2/historicalChainTvl/Ethereum" or "/v2/historicalChainTvl/Solana",
            _ => false
        };
        if (!allowed) throw new ArgumentException("Endpoint is outside the reviewed private-use scope.");
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (stoppedCode is not null) throw new ProviderReadException(stoppedCode);
            try { return await http.GetAsync(path, cancellationToken); }
            catch (ProviderReadException error)
            {
                // An access denial or exhausted budget must not be retried for another asset.
                stoppedCode = error.Code;
                throw;
            }
        }
        finally { gate.Release(); }
    }

    private async Task BeforeAttemptAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Attempts >= requestLimit) throw new ProviderReadException("request-budget-exhausted");
        if (lastAttempt is { } previous)
        {
            var remaining = spacing - Stopwatch.GetElapsedTime(previous);
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        lastAttempt = Stopwatch.GetTimestamp();
        Attempts++;
    }

    public void Dispose() { http.Dispose(); gate.Dispose(); }
}
