using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Analysis.Worker.Tests;

[TestClass]
public sealed class WorkerApplicationTests
{
    [TestMethod]
    public async Task LiveHealthCheckReturnsHealthy()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/health/live", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync());
        Assert.AreEqual(
            "Healthy",
            body.RootElement.GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task ReadyHealthCheckFailsWhenDataDependenciesAreUnavailable()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/health/ready", UriKind.Relative));

        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync());
        Assert.AreEqual(
            "Unhealthy",
            body.RootElement.GetProperty("status").GetString());
    }

    private static WebApplicationFactory<Program> CreateFactory()
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(
                    "ConnectionStrings:Postgres",
                    "Host=127.0.0.1;Port=1;Database=analysis;Username=analysis;Password=test;Timeout=1");
                builder.UseSetting(
                    "ConnectionStrings:Redis",
                    "127.0.0.1:1,connectTimeout=100,syncTimeout=100");
            });
    }
}
