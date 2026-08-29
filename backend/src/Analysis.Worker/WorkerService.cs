namespace Analysis.Worker;

public sealed partial class WorkerService(ILogger<WorkerService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var runId = Guid.NewGuid().ToString("N");

        using var scope = logger.BeginScope(
            new Dictionary<string, object>
            {
                ["RunId"] = runId,
            });

        LogWorkerStarted(logger, runId);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            LogWorkerStopping(logger, runId);
        }
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Worker run {RunId} started; no M2 jobs are registered")]
    private static partial void LogWorkerStarted(
        ILogger logger,
        string runId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Worker run {RunId} is stopping")]
    private static partial void LogWorkerStopping(
        ILogger logger,
        string runId);
}
