using System.Text.Json;
using Analysis.Application;
using Analysis.Infrastructure;
using Analysis.Infrastructure.Adapters;
using Analysis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Worker;

internal static class PrivateIngestion
{
    public static async Task<int> RunAsync(WebApplication app, PrivateIngestionRequest request,
        string runId, CancellationToken cancellationToken)
    {
        await using var db = await app.Services.GetRequiredService<IDbContextFactory<ResearchDbContext>>()
            .CreateDbContextAsync(cancellationToken);
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        {
            app.Logger.LogError("Ingestion refused: apply reviewed M2 migrations with --migrate first");
            return 2;
        }
        var instruments = await db.Instruments.AsNoTracking().OrderBy(i => i.Id).ToArrayAsync(cancellationToken);
        if (!instruments.OrderBy(i => i.Id).SequenceEqual(CatalogSeed.Instruments.OrderBy(i => i.Id)))
        {
            app.Logger.LogError("Ingestion refused: catalog differs from the reviewed eight M2 instrument references");
            return 2;
        }
        using var binance = new PrivateProviderHttp("binance");
        using var bybit = new PrivateProviderHttp("bybit");
        using var defillama = new PrivateProviderHttp("defillama");
        IObservationAdapter[] adapters = [new BinanceMarketAdapter(binance), new BybitDerivativesAdapter(bybit), new DefiLlamaFundamentalsAdapter(defillama)];
        app.Logger.LogInformation("Starting bounded private research ingestion for Kosovo: {StartUtc} to {EndUtc}", request.Window.StartUtc, request.Window.EndUtc);
        var results = await app.Services.GetRequiredService<ObservationIngestion>()
            .RunAsync(instruments, adapters, request.Window, cancellationToken);
        // Log safe counts/codes only. Full provider bytes stay in private PostgreSQL provenance.
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            mode = "private-use", country = "XK", runId, window = request.Window,
            attempts = new { binance = binance.Attempts, bybit = bybit.Attempts, defillama = defillama.Attempts }, results
        }));
        return results.All(r => r.Status == "stored") ? 0 : 1;
    }
}
