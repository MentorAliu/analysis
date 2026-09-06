using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Analysis.Worker;

internal sealed class WorkerHeartbeat : IHealthCheck
{
    private readonly object gate = new();
    private long timestamp;
    private DateTimeOffset? lastProgressUtc;
    private bool running;

    public void Pulse()
    {
        lock (gate)
        {
            timestamp = Stopwatch.GetTimestamp();
            lastProgressUtc = DateTimeOffset.UtcNow;
            running = true;
        }
    }

    public void Stop()
    {
        lock (gate) running = false;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            var healthy = running && Stopwatch.GetElapsedTime(timestamp) < TimeSpan.FromSeconds(10);
            IReadOnlyDictionary<string, object> data = lastProgressUtc is { } last
                ? new Dictionary<string, object> { ["lastProgressUtc"] = last } : new Dictionary<string, object>();
            return Task.FromResult(new HealthCheckResult(healthy ? HealthStatus.Healthy : HealthStatus.Unhealthy, data: data));
        }
    }
}
