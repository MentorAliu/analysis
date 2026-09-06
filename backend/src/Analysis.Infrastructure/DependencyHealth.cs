using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StackExchange.Redis;

namespace Analysis.Infrastructure;

internal sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is 1 ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy();
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or OperationCanceledException)
        {
            // Do not expose connection strings, database names, or provider exceptions.
            return HealthCheckResult.Unhealthy("PostgreSQL probe unavailable.");
        }
    }
}

internal sealed class RedisConnection(string endpoint) : IHostedService, IDisposable
{
    public ConnectionMultiplexer? Client { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var options = ConfigurationOptions.Parse(endpoint);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 1000;
        options.ConnectRetry = 0;
        options.AsyncTimeout = 1000;
        options.BacklogPolicy = BacklogPolicy.FailFast;
        Client = await ConnectionMultiplexer.ConnectAsync(options).WaitAsync(cancellationToken);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Client is not null)
            await Client.CloseAsync(allowCommandsToComplete: false).WaitAsync(cancellationToken);
    }

    public void Dispose() => Client?.Dispose();
}

internal sealed class RedisHealthCheck(RedisConnection connection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            if (connection.Client is null)
                return HealthCheckResult.Degraded("Redis is starting.");
            await connection.Client.GetDatabase().PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException or OperationCanceledException)
        {
            return HealthCheckResult.Degraded("Redis unavailable; optional cache is degraded.");
        }
    }
}
