using Analysis.Hosting;
using Analysis.Worker;

using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddPlatformJsonConsole();

builder.Services.AddPlatformHealthChecks(builder.Configuration);
builder.Services.AddHostedService<WorkerService>();

var app = builder.Build();

app.MapPlatformHealthChecks();

await app.RunAsync();

public partial class Program
{
}
