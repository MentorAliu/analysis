using Analysis.Infrastructure;
using Analysis.Infrastructure.Persistence;
using Analysis.Api.Rankings;

if (args.FirstOrDefault() == "--healthcheck")
    return await Operations.RunProbeAsync(args);

var builder = WebApplication.CreateBuilder(args);
builder.AddOperations();
builder.Services.AddRankingsReads();
builder.Services.AddOpenApi(RankingsOpenApi.Configure);

var app = builder.Build();
app.UseOperations();
app.UseRankingsBoundary();
app.MapOperationalHealth("/api");
app.MapRankings();
if (app.Environment.IsDevelopment())
    app.MapOpenApi("/api/openapi/{documentName}.json");

await app.RunAsync();
return 0;
