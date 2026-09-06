using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Analysis.Infrastructure;

public static class Operations
{
    public static void AddOperations(this WebApplicationBuilder builder)
    {
        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(options =>
        {
            options.IncludeScopes = true;
            options.UseUtcTimestamp = true;
            options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
        });
        builder.Services.Configure<LoggerFactoryOptions>(options =>
            options.ActivityTrackingOptions = ActivityTrackingOptions.TraceId | ActivityTrackingOptions.SpanId);
        builder.Services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(10));
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["traceId"] = Activity.Current?.TraceId.ToString() ?? context.HttpContext.TraceIdentifier;
            context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
        });
        builder.Services.AddExceptionHandler<SanitizedExceptionHandler>();

        var password = builder.Configuration["Postgres:Password"];
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Postgres:Password must be supplied through local configuration.");
        var connection = new NpgsqlConnectionStringBuilder
        {
            Host = builder.Configuration["Postgres:Host"] ?? "postgres",
            Database = builder.Configuration["Postgres:Database"] ?? "analysis",
            Username = builder.Configuration["Postgres:Username"] ?? "analysis",
            Password = password,
            Timeout = 2,
            CommandTimeout = 2,
            Timezone = "UTC",
            IncludeErrorDetail = false
        };
        builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connection.ConnectionString));
        builder.Services.AddSingleton(new RedisConnection(builder.Configuration["Redis:Endpoint"] ?? "redis:6379"));
        builder.Services.AddHostedService(services => services.GetRequiredService<RedisConnection>());
        builder.Services.AddHealthChecks()
            .AddCheck("process", () => HealthCheckResult.Healthy(), tags: ["live", "ready"])
            .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"], timeout: TimeSpan.FromSeconds(3))
            .AddCheck<RedisHealthCheck>("redis", failureStatus: HealthStatus.Degraded, tags: ["ready"], timeout: TimeSpan.FromSeconds(2));
    }

    public static void UseOperations(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var supplied = context.Request.Headers["X-Correlation-ID"];
            var candidate = supplied.Count == 1 ? supplied[0] : null;
            var correlationId = candidate is { Length: > 0 and <= 64 } &&
                candidate.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.')
                    ? candidate : Guid.NewGuid().ToString("N");
            context.TraceIdentifier = correlationId;
            context.Response.OnStarting(() =>
            {
                context.Response.Headers["X-Correlation-ID"] = correlationId;
                return Task.CompletedTask;
            });
            using var scope = app.Logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["TraceId"] = Activity.Current?.TraceId.ToString() ?? correlationId
            });
            await next(context);
            app.Logger.LogInformation("HTTP {Method} completed with {StatusCode}", context.Request.Method, context.Response.StatusCode);
        });
        // Keep errors sanitized in Development as well as Production.
        app.UseExceptionHandler();
        app.UseStatusCodePages();
    }

    public static void MapOperationalHealth(this WebApplication app, string prefix)
    {
        foreach (var kind in new[] { "live", "ready" })
        {
            app.MapGet($"{prefix}/health/{kind}", async (HttpContext context, HealthCheckService healthChecks) =>
            {
                var report = await healthChecks.CheckHealthAsync(registration => registration.Tags.Contains(kind), context.RequestAborted);
                context.Response.Headers.CacheControl = "no-store";
                var result = new OperationalHealthResponse(report.Status.ToString(), DateTimeOffset.UtcNow,
                    report.Entries.ToDictionary(entry => entry.Key,
                        entry => new OperationalCheckResponse(entry.Value.Status.ToString(), entry.Value.Data)));
                return TypedResults.Json(result, statusCode: report.Status == HealthStatus.Unhealthy ? 503 : 200);
            }).WithTags("Operations")
              .Produces<OperationalHealthResponse>()
              .Produces<OperationalHealthResponse>(StatusCodes.Status503ServiceUnavailable);
        }
    }

    // Image probes reuse the installed runtime; no shell, curl, or extra package is needed.
    public static async Task<int> RunProbeAsync(string[] args)
    {
        var path = args.Length > 1 ? args[1] : "/health/ready";
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        try
        {
            using var response = await client.GetAsync($"http://127.0.0.1:8080{path}");
            Console.WriteLine(await response.Content.ReadAsStringAsync());
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Console.WriteLine("Health endpoint unavailable.");
            return 1;
        }
    }
}

public sealed record OperationalHealthResponse(string Status, DateTimeOffset CheckedAtUtc,
    IReadOnlyDictionary<string, OperationalCheckResponse> Checks);

public sealed record OperationalCheckResponse(string Status, IReadOnlyDictionary<string, object> Data);
