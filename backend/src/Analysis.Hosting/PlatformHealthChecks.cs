using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

namespace Analysis.Hosting;

public static class PlatformHealthChecks
{
    private const string LiveTag = "live";
    private const string ReadyTag = "ready";

    public static IHealthChecksBuilder AddPlatformHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var postgresConnectionString =
            GetRequiredConnectionString(configuration, "Postgres");
        var redisConnectionString =
            GetRequiredConnectionString(configuration, "Redis");

        services.AddSingleton(
            _ => CreatePostgresDataSource(postgresConnectionString));
        services.AddSingleton<PostgresHealthCheck>();
        services.AddSingleton(
            _ => new RedisHealthCheck(redisConnectionString));

        return services
            .AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: [LiveTag])
            .AddCheck<PostgresHealthCheck>(
                "postgres",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadyTag],
                timeout: TimeSpan.FromSeconds(2))
            .AddCheck<RedisHealthCheck>(
                "redis",
                failureStatus: HealthStatus.Unhealthy,
                tags: [ReadyTag],
                timeout: TimeSpan.FromSeconds(2));
    }

    public static IEndpointRouteBuilder MapPlatformHealthChecks(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapHealthChecks(
                "/health/live",
                new HealthCheckOptions
                {
                    Predicate = registration => registration.Tags.Contains(LiveTag),
                    ResponseWriter = WriteResponseAsync,
                })
            .ShortCircuit();

        endpoints
            .MapHealthChecks(
                "/health/ready",
                new HealthCheckOptions
                {
                    Predicate = registration => registration.Tags.Contains(ReadyTag),
                    ResponseWriter = WriteResponseAsync,
                })
            .ShortCircuit();

        return endpoints;
    }

    private static Task WriteResponseAsync(
        HttpContext context,
        HealthReport report)
    {
        context.Response.ContentType = "application/json";

        return context.Response.WriteAsJsonAsync(
            new HealthResponse(report.Status.ToString(), DateTimeOffset.UtcNow),
            cancellationToken: context.RequestAborted);
    }

    private static string GetRequiredConnectionString(
        IConfiguration configuration,
        string name)
    {
        var connectionString = configuration.GetConnectionString(name);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{name}' is required");
        }

        return connectionString;
    }

    private static NpgsqlDataSource CreatePostgresDataSource(string connectionString)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        // Official Npgsql 10 default is Prefer. Alpine runtime images do not
        // ship Kerberos, and Prefer then logs a missing libgssapi_krb5.so.2.
        dataSourceBuilder.ConnectionStringBuilder.GssEncryptionMode =
            GssEncryptionMode.Disable;

        return dataSourceBuilder.Build();
    }

    private sealed record HealthResponse(
        string Status,
        DateTimeOffset CheckedAtUtc);
}
