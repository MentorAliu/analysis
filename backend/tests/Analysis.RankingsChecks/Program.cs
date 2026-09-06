using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Analysis.Api.Rankings;
using Analysis.Application;
using Analysis.Domain.Scoring;
using Analysis.Infrastructure;
using Analysis.Infrastructure.Persistence;
using Analysis.ScoringChecks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Analysis.RankingsChecks;

internal static class Program
{
    public static int Count { get; private set; }
    public static void Check(bool value, string message)
    { if (!value) throw new InvalidOperationException(message); Count++; }
    public static async Task<int> Main(string[] args)
    {
        try
        {
            if (args is ["--database-checks"]) { await DatabaseChecks.Run(); return 0; }
            if (args.Length != 0 && args is not ["--export", _, _]) throw new ArgumentException("Unknown check arguments");
            UnitChecks();
            await HttpChecks(args);
            Console.WriteLine(JsonSerializer.Serialize(new { mode = "m4-checks", assertions = Count,
                manifestHash = ScoringModel.Slice1.Hash, sourceHash = ScoringModel.Slice1.SourceHash }));
            return 0;
        }
        catch (Exception e) { Console.Error.WriteLine($"FAIL {e.GetType().Name}: {e.Message}"); return 1; }
    }
    internal static RankingsReadBatch Fixture()
    {
        var m = ScoringModel.Slice1;
        var calculations = ScoringJobs.Calculate(Synthetic.Input(), m);
        var items = calculations.Assets.Select(a =>
        {
            int CountState(string state) => a.Features.Values.Count(f => f.State == state);
            return new RankingReadItem(CatalogSeed.Assets.Single(s => s.Id == a.Score.AssetId), new('a', 64), new('a', 64), new('b', 64), new('c', 64),
                a.Features.CorePriceReady, new(CountState("available"), CountState("missing"), CountState("stale"), CountState("invalid"), CountState("conflicted"), CountState("inapplicable")), a.Score);
        }).ToArray();
        return new(new('d', 64), Synthetic.T, Synthetic.K, Synthetic.K, "research-reconstruction", new('e', 64),
            m.Manifest.Universe, m.Manifest, m.Hash, m.SourceHash, items);
    }
    private static void UnitChecks()
    {
        var fixture = Fixture(); var request = new RankingsRequest("slice1-v1", null);
        foreach (var culture in new[] { "en-US", "hu-HU", "tr-TR" })
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            var mapped = RankingTransport.Map(request, fixture with { Items = fixture.Items.Reverse().ToArray() }, Synthetic.K);
            Check(mapped.Items.Select(i => i.AssetId).SequenceEqual(new[] { "bitcoin", "ethereum", "solana" }), "Ordinal tie ordering");
            Check(mapped.Items.Select(i => i.Rank).SequenceEqual(new int?[] { 1, 2, 3 }), "Unique ranks");
            Check(mapped.Items.All(i => i.CompositeScore == "0.000000"), "Exact neutral decimals in every culture");
            Check(mapped.Items[0].Categories.Where(c => c.State == CategoryState.inapplicable).Count(c => c.Score is null && c.DataQualityPercent == "0.000000") == 2, "BTC inapplicability retained");
        }
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var partial = fixture.Items[2] with { Score = fixture.Items[2].Score with { State = "partial", Composite = 20.123456m, ContextCoverage = 50 } };
        var notReady = fixture.Items[0] with { CorePriceReady = false, Score = fixture.Items[0].Score with
        { State = "not-ready", Composite = null, BullishConfidence = null, BearishConfidence = null } };
        var mixed = RankingTransport.Map(request, fixture with { Items = [notReady, fixture.Items[1], partial] }, Synthetic.K);
        Check(mixed.Items[0].AssetId == "solana" && mixed.Items[0].CompositeScore == "20.123456", "Partial outranks complete without new penalty");
        Check(mixed.Items[2].Rank is null && mixed.Items[2].CompositeScore is null, "Not-ready unranked last");
        var absent = fixture.Items.Select(i => i with { CorePriceReady = false, Score = i.Score with { State = "not-ready", Composite = null, BullishConfidence = null, BearishConfidence = null } }).ToArray();
        Check(RankingTransport.Map(request, fixture with { Items = absent }, Synthetic.K).Items.All(i => i.Rank is null), "All-not-ready retained");
        foreach (var value in new[] { -100m, -0.000001m, 0m, 0.000001m, 100m })
            Check(decimal.Parse(RankingTransport.Decimal(value, true), CultureInfo.InvariantCulture) == value, "Decimal exact round trip");
        foreach (var value in new[] { 100.000001m, 0.0000001m, -100.000001m })
        { try { RankingTransport.Decimal(value, true); throw new Exception("Accepted invalid precision/range"); } catch (RankingsReadException) { Count++; } }
        Check(RankingTransport.Timestamp(Synthetic.K.AddMilliseconds(123)).EndsWith(".123Z", StringComparison.Ordinal), "UTC millisecond preservation");
    }
    private static async Task HttpChecks(string[] args)
    {
        var stub = new Stub();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], EnvironmentName = "Development", ApplicationName = typeof(RankingsEndpoint).Assembly.GetName().Name });
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Postgres:Password"] = "unused-synthetic", ["Redis:Endpoint"] = "127.0.0.1:1", ["Rankings:PrivateUseEnabled"] = "true" });
        builder.AddOperations(); builder.Logging.SetMinimumLevel(LogLevel.Error);
        builder.Services.AddSingleton<IRankingsReader>(stub); builder.Services.AddSingleton<TimeProvider>(new Synthetic.Clock(Synthetic.K));
        builder.Services.AddOpenApi(RankingsOpenApi.Configure);
        await using var app = builder.Build(); app.UseOperations(); app.UseRankingsBoundary(); app.MapOperationalHealth("/api"); app.MapRankings();
        app.MapOpenApi("/api/openapi/{documentName}.json");
        await app.StartAsync();
        try
        {
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var client = new HttpClient { BaseAddress = new(address) };
            var schema = await client.GetStringAsync("/api/openapi/v1.json");
            using var document = JsonDocument.Parse(schema);
            Check(document.RootElement.GetProperty("openapi").GetString()!.StartsWith("3.1", StringComparison.Ordinal), "Actual OpenAPI 3.1");
            var endpoint = document.RootElement.GetProperty("paths").GetProperty(RankingsEndpoint.Path).GetProperty("get");
            Check(endpoint.GetProperty("operationId").GetString() == "GetRankings", "Stable operation ID");
            Check(endpoint.GetProperty("parameters").GetArrayLength() == 2, "Only two query parameters");
            foreach (var invalid in new[] { "?modelId=", "?modelId=A", "?modelId=slice1-v1%0A", "?ModelId=slice1-v1", "?modelId=a&modelId=b", "?modelId=a&ModelId=b", "?sort=asc", "?asOfUtc=2021-01-08", "?asOfUtc=2021-02-30T00:00:00Z", "?asOfUtc=2021-01-08T00:00:00%2B00:00", "?asOfUtc=2021-01-08T00:30:00Z", "?asOfUtc=2099-01-01T00:00:00Z", "?asOfUtc=", "?asOfUtc=2021-01-08T00:00:00Z&asOfUtc=2021-01-08T00:00:00Z", "?modelId=%20slice1-v1", "?modelId=" + new string('a', 65) })
            {
                var before = stub.Calls;
                using var response = await client.GetAsync(RankingsEndpoint.Path + invalid);
                await CheckProblem(response, 400);
                Check(stub.Calls == before, "Invalid request does not read database");
            }
            using var good = await client.GetAsync(RankingsEndpoint.Path);
            Check(good.IsSuccessStatusCode && good.Headers.CacheControl?.NoStore == true, "Successful no-store response");
            var body = await good.Content.ReadAsStringAsync();
            Check(stub.Last == new RankingsRequest("slice1-v1", null), "Explicit default model, latest selection");
            using var exact = await client.GetAsync(RankingsEndpoint.Path + "?asOfUtc=2021-01-08T00:00:00Z&modelId=stored-v2");
            Check(stub.Last == new RankingsRequest("stored-v2", Synthetic.T), "Explicit model and exact hour passed unchanged");
            foreach (var (code, status) in new[] { ("model-not-found", 404), ("batch-not-found", 404), ("schema-not-ready", 503), ("rankings-integrity-failure", 500) })
            {
                stub.Error = new RankingsReadException(code);
                await CheckProblem(await client.GetAsync(RankingsEndpoint.Path), status);
            }
            stub.Error = new TimeoutException("secret-sentinel");
            await CheckProblem(await client.GetAsync(RankingsEndpoint.Path), 503);
            stub.Error = new InvalidOperationException("secret-sentinel");
            using var unexpected = new HttpRequestMessage(HttpMethod.Get, RankingsEndpoint.Path);
            unexpected.Headers.Add("Accept", "text/html"); unexpected.Headers.Add("X-Correlation-ID", "m4-check");
            using var failure = await client.SendAsync(unexpected); await CheckProblem(failure, 500);
            Check(failure.Headers.GetValues("X-Correlation-ID").Single() == "m4-check", "Correlation survives failure");
            stub.Error = null;
            await CheckProblem(await client.PostAsync(RankingsEndpoint.Path, null), 405);
            app.Configuration["Rankings:PrivateUseEnabled"] = "false"; var calls = stub.Calls;
            await CheckProblem(await client.GetAsync(RankingsEndpoint.Path), 403);
            Check(stub.Calls == calls, "Disabled endpoint performs no read");
            app.Configuration["Rankings:PrivateUseEnabled"] = "true";
            stub.Block = true;
            using var abort = new CancellationTokenSource(); var pending = client.GetAsync(RankingsEndpoint.Path, abort.Token);
            await stub.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5)); abort.Cancel();
            try { await pending; throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) { Count++; }
            await stub.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5)); Count++;
            if (args is ["--export", var schemaPath, var fixturePath])
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(schemaPath)!);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fixturePath)!);
                await File.WriteAllTextAsync(schemaPath, schema + "\n");
                await File.WriteAllTextAsync(fixturePath, body + "\n");
            }
        }
        finally { await app.StopAsync(); }
    }
    private static async Task CheckProblem(HttpResponseMessage response, int status)
    {
        using (response)
        {
            Check((int)response.StatusCode == status, $"Expected problem status {status}");
            Check(response.Content.Headers.ContentType?.MediaType == "application/problem+json", "Problem content type");
            var text = await response.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(text);
            Check(json.RootElement.GetProperty("status").GetInt32() == status && json.RootElement.TryGetProperty("traceId", out _) &&
                json.RootElement.TryGetProperty("correlationId", out _), "Problem status/trace/correlation");
            Check(!text.Contains("secret-sentinel", StringComparison.Ordinal) && !text.Contains("stackTrace", StringComparison.Ordinal), "Sanitized errors");
        }
    }
    private sealed class Stub : IRankingsReader
    {
        public int Calls; public RankingsRequest? Last; public Exception? Error; public bool Block;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Cancelled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<RankingsReadBatch> ReadAsync(RankingsRequest request, CancellationToken token)
        {
            Calls++; Last = request; if (Error is not null) throw Error;
            if (Block)
            {
                Entered.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
                catch (OperationCanceledException) { Cancelled.TrySetResult(); throw; }
            }
            return Fixture();
        }
    }
}
