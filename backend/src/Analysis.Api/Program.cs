using Analysis.Api;
using Analysis.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddPlatformJsonConsole();

builder.Services.AddOpenApi();
builder.Services.AddPlatformHealthChecks(builder.Configuration);
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["correlationId"] =
            context.HttpContext.TraceIdentifier;
    };
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

app.MapOpenApi("/openapi/{documentName}.json");
app.MapPlatformHealthChecks();

app.MapGet(
        "/",
        () => new ApiMetadataResponse(
            "Analysis.Api",
            "M1",
            "Crypto intelligence research API"))
    .WithName("GetApiMetadata");

await app.RunAsync();

public partial class Program
{
}
