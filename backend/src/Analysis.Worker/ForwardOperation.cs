using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Analysis.Application;
using Analysis.Domain;
using Analysis.Domain.Scoring;
using Analysis.Infrastructure;
using Analysis.Infrastructure.Adapters;
using Analysis.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Analysis.Worker;

internal static class ForwardOperation
{
    public static async Task<int> RunAsync(WebApplication app, ForwardCommand command, string runId, CancellationToken ct)
    {
        var factory = app.Services.GetRequiredService<IDbContextFactory<ResearchDbContext>>();
        // Inspection is strictly read-only, including operational audit tables.
        if (command.Operation == "--inspect-forward-records")
        {
            var report = await app.Services.GetRequiredService<IForwardRecordReader>().InspectAsync(command.Range, ct);
            Print(new { mode = "forward-inspect", runId, report }); return 0;
        }
        await EventAsync(factory, command, runId, "started", new { }, ct);
        try
        {
            object result; var exit = 0;
            if (command.Operation == "--issue-forward-once")
            {
                var issue = await app.Services.GetRequiredService<ForwardRecordingJobs>().IssueAsync("slice1-v1", command.StartUtc, ct);
                result = issue; exit = issue.Original.EligibleAssetIds.Length == 0 ? 3 : 0;
            }
            else if (command.Operation == "--measure-outcomes-once")
                result = await app.Services.GetRequiredService<ForwardOutcomeJobs>().MeasureAsync(command.Range, ct);
            else
            {
                var store = app.Services.GetRequiredService<IForwardCollectionStore>();
                var work = command.Operation == "--collect-forward-inputs-once" ? await store.PlanInputsAsync(command.StartUtc, ct) :
                    await store.PlanOutcomesAsync(new(command.StartUtc, command.EndUtc), ct);
                if (work.Length == 0) result = new { work = 0, results = Array.Empty<ForwardCollectionResult>() };
                else
                {
                    // Construct only the providers present in the validated bounded plan.
                    var transports = work.Select(w => w.Instrument.ProviderId).Distinct().ToDictionary(id => id, id => new PrivateProviderHttp(id));
                    try
                    {
                        var adapters = transports.Select(pair => pair.Key switch
                        {
                            "binance" => (IObservationAdapter)new BinanceMarketAdapter(pair.Value),
                            "bybit" => new BybitDerivativesAdapter(pair.Value),
                            "defillama" => new DefiLlamaFundamentalsAdapter(pair.Value),
                            _ => throw new ForwardPreconditionException("unsupported-provider")
                        }).ToArray();
                        var results = await app.Services.GetRequiredService<ForwardCollection>().CollectAsync(work, adapters, ct);
                        exit = results.All(r => r.Status == "stored") ? 0 : 3;
                        result = new { results, attempts = transports.ToDictionary(p => p.Key, p => p.Value.Attempts) };
                    }
                    finally { foreach (var transport in transports.Values) transport.Dispose(); }
                }
            }
            var detail = result is ForwardIssueResult issueResult
                ? (object)new { exitCode = exit, issuanceId = issueResult.Original.Id, duplicate = issueResult.Duplicate,
                    eligible = issueResult.Original.EligibleAssetIds.Length }
                : new { exitCode = exit, result };
            await EventAsync(factory, command, runId, "completed", detail, ct);
            Print(new { mode = command.Operation[2..], runId, result }); return exit;
        }
        catch (Exception error)
        {
            // A separate finite token permits recording cancellation without keeping the process alive indefinitely.
            using var audit = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await EventAsync(factory, command, runId, error is OperationCanceledException ? "cancelled" : "failed",
                new { code = error is ForwardPreconditionException or ScoringPreconditionException ? error.Message : error.GetType().Name }, audit.Token); }
            catch (Exception auditError) { app.Logger.LogWarning("Forward audit completion unavailable: {ErrorType}", auditError.GetType().Name); }
            throw;
        }
    }
    private static async Task EventAsync(IDbContextFactory<ResearchDbContext> factory, ForwardCommand command,
        string runId, string state, object detail, CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await ScoringStore.PreconditionsAsync(db, ct);
        var now = await db.Database.SqlQuery<DateTimeOffset>($"SELECT research.\"ForwardClock\"() AS \"Value\"").SingleAsync(ct);
        db.Add(new ForwardRunEventRow { Id = CanonicalJson.Hash(runId + ":" + state), RunId = runId, Operation = command.Operation,
            State = state, AtUtc = now, StartUtc = command.StartUtc, EndUtc = command.EndUtc, DetailJson = CanonicalJson.Write(detail) });
        await db.SaveChangesAsync(ct);
    }
    private static void Print(object value)
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new DecimalStrings()); options.Converters.Add(new UtcStrings());
        Console.WriteLine(JsonSerializer.Serialize(value, options));
    }
    private sealed class DecimalStrings : JsonConverter<decimal>
    {
        public override decimal Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
    }
    private sealed class UtcStrings : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => throw new NotSupportedException();
        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        { Utc.Require(value); writer.WriteStringValue(value.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)); }
    }
}
