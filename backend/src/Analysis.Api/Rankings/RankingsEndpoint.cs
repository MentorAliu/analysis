using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Analysis.Application;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Analysis.Api.Rankings;

public static class RankingsEndpoint
{
    public const string Path = "/api/v1/rankings";
    public const string DefaultModel = "slice1-v1";
    public static void UseRankingsBoundary(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.Path.Value!.TrimEnd('/').Equals(Path, StringComparison.OrdinalIgnoreCase)) { await next(context); return; }
            context.Response.Headers.CacheControl = "no-store";
            try
            {
                if (!app.Configuration.GetValue<bool>("Rankings:PrivateUseEnabled"))
                { await Problem(context, 403, "private-use-disabled", "Private rankings access is disabled.").ExecuteAsync(context); return; }
                if (!HttpMethods.IsGet(context.Request.Method))
                {
                    context.Response.Headers.Allow = "GET";
                    await Problem(context, 405, "method-not-allowed", "Only GET is supported.").ExecuteAsync(context); return;
                }
                await next(context);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            { if (!context.Response.HasStarted) context.Response.StatusCode = 499; }
        });
    }
    public static void MapRankings(this WebApplication app)
    {
        app.MapGet(Path, async (HttpContext context, IRankingsReader reader, TimeProvider clock,
            [FromQuery] string? modelId, [FromQuery] string? asOfUtc) =>
        {
            // Inspect the complete query collection so scalar binding cannot hide duplicates.
            var request = Parse(context.Request.Query, clock.GetUtcNow(), out var errors);
            if (request is null) return Problem(context, 400, "invalid-query", "The rankings query is invalid.", errors);
            try
            {
                var batch = await reader.ReadAsync(request, context.RequestAborted);
                var now = DateTimeOffset.FromUnixTimeMilliseconds(clock.GetUtcNow().ToUnixTimeMilliseconds());
                return (IResult)TypedResults.Ok(RankingTransport.Map(request, batch, now));
            }
            catch (RankingsReadException e)
            {
                var status = e.Code switch { "model-not-found" or "batch-not-found" => 404, "schema-not-ready" => 503, _ => 500 };
                if (status == 500) app.Logger.LogError("Rankings read failed integrity validation");
                return Problem(context, status, e.Code, status switch { 404 => "The requested rankings resource does not exist.",
                    503 => "The rankings schema is not ready.", _ => "Stored rankings failed integrity validation." });
            }
            catch (Exception e) when (e is NpgsqlException or TimeoutException)
            {
                context.RequestAborted.ThrowIfCancellationRequested();
                app.Logger.LogWarning("Rankings database unavailable ({ExceptionType})", e.GetType().Name);
                return Problem(context, 503, "database-unavailable", "The rankings database is unavailable.");
            }
        }).WithName("GetRankings").WithTags("Rankings")
          .WithSummary("Read one private persisted ranking batch")
          .WithDescription("Historical as-of requires an exact stored UTC hour. Default model is slice1-v1. Results are research reconstructions, not probabilities or contemporaneously issued signals.")
          .Produces<RankingsResponse>()
          .Produces<RankingsProblem>(400, "application/problem+json")
          .Produces<RankingsProblem>(403, "application/problem+json")
          .Produces<RankingsProblem>(404, "application/problem+json")
          .Produces<RankingsProblem>(405, "application/problem+json")
          .Produces<RankingsProblem>(500, "application/problem+json")
          .Produces<RankingsProblem>(503, "application/problem+json");
    }
    public static RankingsRequest? Parse(IQueryCollection query, DateTimeOffset now, out Dictionary<string, string[]> errors)
    {
        errors = new(StringComparer.Ordinal);
        foreach (var pair in query)
        {
            if (pair.Key is not ("modelId" or "asOfUtc")) errors["_query"] = ["Only modelId and asOfUtc are supported, with exact casing."];
            else if (pair.Value.Count != 1 || string.IsNullOrEmpty(pair.Value[0])) errors[pair.Key] = ["Supply one non-empty value."];
        }
        var model = query.ContainsKey("modelId") ? query["modelId"].ToString() : DefaultModel;
        if (!Regex.IsMatch(model, Wire.ModelId, RegexOptions.CultureInvariant)) errors["modelId"] = ["Expected a lowercase model ID of 1-64 characters."];
        DateTimeOffset? asOf = null;
        if (query.ContainsKey("asOfUtc"))
        {
            var raw = query["asOfUtc"].ToString();
            if (!Regex.IsMatch(raw, Wire.Hour, RegexOptions.CultureInvariant) ||
                !DateTimeOffset.TryParseExact(raw, "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed) || parsed > now)
                errors["asOfUtc"] = ["Expected a real UTC hour YYYY-MM-DDTHH:00:00Z no later than now."];
            else asOf = parsed;
        }
        return errors.Count == 0 ? new(model, asOf) : null;
    }
    private static IResult Problem(HttpContext context, int status, string code, string title, Dictionary<string, string[]>? errors = null) =>
        TypedResults.Json(new RankingsProblem($"urn:analysis:problem:{code}", title, status, context.TraceIdentifier,
            Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier, Path, code, errors),
            statusCode: status, contentType: "application/problem+json");
}
