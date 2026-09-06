using Analysis.Infrastructure;
using Analysis.Infrastructure.Persistence;
using Analysis.Worker;
using Microsoft.EntityFrameworkCore;

if (args.FirstOrDefault() == "--healthcheck")
    return await Operations.RunProbeAsync(args);

if (args.FirstOrDefault() == "--ingest-once")
{
    Console.Error.WriteLine("Live ingestion is disabled: provider licensing, regional access and current coverage remain unresolved. See the active M2 plan.");
    return 2;
}

var maintenance = args is ["--migrate"];
var builder = WebApplication.CreateBuilder(maintenance ? [] : args);
builder.AddOperations();
builder.Services.AddResearchPersistence();
builder.Services.AddSingleton<WorkerHeartbeat>();
builder.Services.AddHostedService<LifecycleWorker>();
builder.Services.AddHealthChecks().AddCheck<WorkerHeartbeat>("worker-loop", tags: ["live", "ready"]);

var app = builder.Build();
if (maintenance)
{
    using var cancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler cancel = (_, eventArgs) => { eventArgs.Cancel = true; cancellation.Cancel(); };
    Console.CancelKeyPress += cancel;
    using var signal = System.Runtime.InteropServices.PosixSignalRegistration.Create(
        System.Runtime.InteropServices.PosixSignal.SIGTERM, context => { context.Cancel = true; cancellation.Cancel(); });
    try
    {
        var runId = Guid.NewGuid().ToString("N");
        using var scope = app.Logger.BeginScope(new Dictionary<string, object> { ["RunId"] = runId, ["CorrelationId"] = runId });
        await using var database = await app.Services.GetRequiredService<IDbContextFactory<ResearchDbContext>>()
            .CreateDbContextAsync(cancellation.Token);
        database.Database.SetCommandTimeout(60);
        await database.Database.MigrateAsync(cancellation.Token);
        app.Logger.LogInformation("M2 schema migrations applied; live provider use remains disabled");
        return 0;
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
