using Analysis.Infrastructure;

if (args.FirstOrDefault() == "--healthcheck")
    return await Operations.RunProbeAsync(args);

var builder = WebApplication.CreateBuilder(args);
builder.AddOperations();
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseOperations();
app.MapOperationalHealth("/api");
if (app.Environment.IsDevelopment())
    app.MapOpenApi("/api/openapi/{documentName}.json");

await app.RunAsync();
return 0;
