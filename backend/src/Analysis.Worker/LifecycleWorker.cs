using System.Diagnostics;

namespace Analysis.Worker;

internal sealed class LifecycleWorker(WorkerHeartbeat heartbeat, ILogger<LifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = new Activity("worker-lifecycle").Start();
        var runId = Guid.NewGuid().ToString("N");
        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["RunId"] = runId,
            ["CorrelationId"] = runId,
            ["TraceId"] = activity.TraceId.ToString()
        });
        logger.LogInformation("Worker lifecycle started; no analysis jobs are configured");
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            do { heartbeat.Pulse(); }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Cancellation is normal shutdown, not a failed analysis run.
        }
        finally
        {
            heartbeat.Stop();
            logger.LogInformation("Worker lifecycle stopped gracefully");
        }
    }
}
