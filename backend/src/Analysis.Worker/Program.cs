using Analysis.Infrastructure;
using Analysis.Application;
using Analysis.Infrastructure.Persistence;
using Analysis.Worker;
using Microsoft.EntityFrameworkCore;

if (args.FirstOrDefault() == "--healthcheck")
    return await Operations.RunProbeAsync(args);

PrivateIngestionRequest? ingestion = null;
var ingestOnce = args.FirstOrDefault() == "--ingest-once";
if (ingestOnce && !PrivateIngestionRequest.TryParse(args, DateTimeOffset.UtcNow, out ingestion))
{
    Console.Error.WriteLine($"Live ingestion is disabled without the reviewed private-use scope and a closed hourly UTC window of at most seven days. Usage: {PrivateIngestionRequest.Usage}");
    return 2;
}

var maintenance = args is ["--migrate"];
ScoringCommand? scoring = null;
var scoringCommand = args.FirstOrDefault() is "--score-once" or "--replay-scores";
if (scoringCommand && !ScoringCommand.TryParse(args, DateTimeOffset.UtcNow, out scoring))
{
    Console.Error.WriteLine($"Invalid M3 command. Usage: {ScoringCommand.Usage}");
    return 2;
}
var builder = WebApplication.CreateBuilder(maintenance || ingestOnce || scoringCommand ? [] : args);
builder.AddOperations();
builder.Services.AddResearchPersistence();
builder.Services.AddSingleton<WorkerHeartbeat>();
builder.Services.AddHostedService<LifecycleWorker>();
builder.Services.AddHealthChecks().AddCheck<WorkerHeartbeat>("worker-loop", tags: ["live", "ready"]);

var app = builder.Build();
if (maintenance || ingestOnce || scoringCommand)
{
    using var cancellation = new CancellationTokenSource();
    cancellation.CancelAfter(TimeSpan.FromMinutes(5));
    ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    Console.CancelKeyPress += cancel;
    using var signal = System.Runtime.InteropServices.PosixSignalRegistration.Create(
        System.Runtime.InteropServices.PosixSignal.SIGTERM, context => { context.Cancel = true; cancellation.Cancel(); });
    var runId = Guid.NewGuid().ToString("N");
    using var scope = app.Logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId, ["CorrelationId"] = runId });
    try
    {
        if (ingestion is not null)
            return await PrivateIngestion.RunAsync(app, ingestion, runId, cancellation.Token);
        if (scoring is not null)
            return await ScoringOperation.RunAsync(app, scoring, runId, cancellation.Token);
        await using var database = await app.Services.GetRequiredService<IDbContextFactory<ResearchDbContext>>()
            .CreateDbContextAsync(cancellation.Token);
        database.Database.SetCommandTimeout(60);
        await database.Database.MigrateAsync(cancellation.Token);
        app.Logger.LogInformation("Reviewed schema migrations applied; provider access requires a separate explicit private-use command");
        return 0;
    }
    catch (ScoringPreconditionException error)
    {
        app.Logger.LogError("M3 precondition failed: {Code}", error.Message);
        return 2;
    }
    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
    {
        app.Logger.LogInformation("{Milestone} one-shot operation cancelled or its five-minute deadline elapsed", scoringCommand ? "M3" : "M2");
        return 130;
    }
    catch (Exception error)
    {
        app.Logger.LogError("{Milestone} one-shot operation failed: {ErrorType}; no exception payload logged", scoringCommand ? "M3" : "M2", error.GetType().Name);
        return 1;
    }
    finally
    {
        Console.CancelKeyPress -= cancel;
        await app.DisposeAsync();
    }
}
app.UseOperations();
app.MapOperationalHealth("");
await app.RunAsync();
return 0;
