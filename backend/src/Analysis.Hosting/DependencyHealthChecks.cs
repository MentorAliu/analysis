using Microsoft.Extensions.Diagnostics.HealthChecks;

using Npgsql;

using StackExchange.Redis;

namespace Analysis.Hosting;

internal sealed class PostgresHealthCheck(NpgsqlDataSource dataSource)
    : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection =
                await dataSource.OpenConnectionAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (NpgsqlException exception)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "PostgreSQL is unavailable",
                exception);
        }
        catch (TimeoutException exception)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "PostgreSQL health check timed out",
                exception);
        }
    }
}

internal sealed class RedisHealthCheck : IHealthCheck, IAsyncDisposable
{
    private readonly Lazy<Task<ConnectionMultiplexer>> connection;

    public RedisHealthCheck(string connectionString)
    {
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 1;
        options.ConnectTimeout = Math.Min(options.ConnectTimeout, 3_000);

        connection = new Lazy<Task<ConnectionMultiplexer>>(
            () => ConnectionMultiplexer.ConnectAsync(options));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var multiplexer =
                await connection.Value.WaitAsync(cancellationToken);
            await multiplexer
                .GetDatabase()
                .PingAsync()
                .WaitAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (RedisException exception)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Redis is unavailable",
                exception);
        }
        catch (TimeoutException exception)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "Redis health check timed out",
                exception);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!connection.IsValueCreated)
        {
            return;
        }

        try
        {
            var multiplexer = await connection.Value;
            await multiplexer.DisposeAsync();
        }
        catch (RedisException)
        {
            // A failed initial connection has no live resources to dispose.
        }
    }
}
