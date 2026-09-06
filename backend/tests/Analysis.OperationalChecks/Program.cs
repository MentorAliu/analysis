using System.Net;
using System.Text.Json;
using Analysis.Infrastructure;
using Analysis.Worker;
using Microsoft.Extensions.Diagnostics.HealthChecks;

// Executable behavioral checks use only the pinned shared framework. Test routes
// exist in this process only; they are never part of either application image.
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration["HTTP_PORTS"] = "";
builder.Configuration["Logging:LogLevel:Microsoft.AspNetCore"] = "Warning";
builder.Configuration["Postgres:Password"] = "test-only-not-a-credential";
builder.Configuration["Postgres:Host"] = "127.0.0.1";
builder.Configuration["Redis:Endpoint"] = "127.0.0.1:1";
builder.AddOperations();
await using var app = builder.Build();
app.UseOperations();
var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
var cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
app.MapGet("/throw", (Func<IResult>)(() => throw new InvalidOperationException("private-exception-sentinel")));
app.MapGet("/slow", async (CancellationToken token) =>
{
    entered.TrySetResult();
    try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
    catch (OperationCanceledException) when (token.IsCancellationRequested) { cancelled.TrySetResult(); }
});
await app.StartAsync();
try
{
    using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
    foreach (var (path, method, status) in new[]
    {
        ("/missing", HttpMethod.Get, HttpStatusCode.NotFound),
        ("/throw", HttpMethod.Post, HttpStatusCode.MethodNotAllowed),
        ("/throw", HttpMethod.Get, HttpStatusCode.InternalServerError)
    })
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Correlation-ID", "m1-correlation-check");
        request.Headers.Add("Accept", "application/json");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        Require(response.StatusCode == status, $"{path} HTTP status");
        Require(response.Content.Headers.ContentType?.MediaType == "application/problem+json", "problem content type");
        Require(json.RootElement.GetProperty("status").GetInt32() == (int)status, "problem status");
        Require(json.RootElement.GetProperty("correlationId").GetString() == "m1-correlation-check", "problem correlation");
        Require(json.RootElement.GetProperty("traceId").GetString()?.Length == 32, "problem trace");
        Require(response.Headers.GetValues("X-Correlation-ID").Single() == "m1-correlation-check", "response correlation");
        Require(!body.Contains("private-exception-sentinel") && !body.Contains("stackTrace"), "sanitized problem body");
    }
    using (var invalid = new HttpRequestMessage(HttpMethod.Get, "/missing"))
    {
        invalid.Headers.Add("X-Correlation-ID", new string('x', 100));
        using var response = await client.SendAsync(invalid);
        Require(response.Headers.GetValues("X-Correlation-ID").Single().Length == 32, "untrusted correlation replaced");
    }
    using (var html = new HttpRequestMessage(HttpMethod.Get, "/throw"))
    {
        html.Headers.Add("Accept", "text/html");
        using var response = await client.SendAsync(html);
        Require(response.StatusCode == HttpStatusCode.InternalServerError, "unsupported Accept preserves 500");
        Require(response.Content.Headers.ContentType?.MediaType == "application/problem+json", "unsupported Accept sanitized fallback");
        Require(!(await response.Content.ReadAsStringAsync()).Contains("private-exception-sentinel"), "fallback sanitized");
    }
    using var abort = new CancellationTokenSource();
    var pending = client.GetAsync("/slow", abort.Token);
    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
    abort.Cancel();
    try { await pending; throw new Exception("Request cancellation was ignored."); }
    catch (OperationCanceledException) when (abort.IsCancellationRequested) { }
    await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Console.WriteLine("PASS problem details 404/405/500, correlation normalization, request cancellation");

    var heartbeat = new WorkerHeartbeat();
    var context = new HealthCheckContext();
    Require((await heartbeat.CheckHealthAsync(context)).Status == HealthStatus.Unhealthy, "unstarted worker unhealthy");
    heartbeat.Pulse();
    Require((await heartbeat.CheckHealthAsync(context)).Status == HealthStatus.Healthy, "active worker healthy");
    await Task.Delay(TimeSpan.FromSeconds(11));
    Require((await heartbeat.CheckHealthAsync(context)).Status == HealthStatus.Unhealthy, "stalled worker unhealthy");
    heartbeat.Pulse();
    Require((await heartbeat.CheckHealthAsync(context)).Status == HealthStatus.Healthy, "worker progress recovery");
    heartbeat.Stop();
    Require((await heartbeat.CheckHealthAsync(context)).Status == HealthStatus.Unhealthy, "stopped worker unhealthy");
    Console.WriteLine("PASS worker health unstarted, active, stalled, recovered, stopped");
}
finally
{
    using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    await app.StopAsync(stop.Token);
}

static void Require(bool value, string message)
{
    if (!value) throw new Exception($"FAIL {message}");
}
