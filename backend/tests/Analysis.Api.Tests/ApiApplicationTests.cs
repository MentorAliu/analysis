using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

using Analysis.Hosting;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Analysis.Api.Tests;

[TestClass]
public sealed class ApiApplicationTests
{
    [TestMethod]
    public async Task LiveHealthCheckReturnsUtcJson()
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

        var checkedAtUtc = body.RootElement
            .GetProperty("checkedAtUtc")
            .GetDateTimeOffset();
        Assert.AreEqual(TimeSpan.Zero, checkedAtUtc.Offset);
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

    [TestMethod]
    public async Task UnknownRouteReturnsProblemDetailsWithCorrelationId()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("/does-not-exist", UriKind.Relative));
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
        Assert.AreEqual(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var body = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync());
        Assert.AreEqual(
            StatusCodes.Status404NotFound,
            body.RootElement.GetProperty("status").GetInt32());
        Assert.IsTrue(
            body.RootElement.TryGetProperty("correlationId", out var correlationId));
        Assert.IsFalse(string.IsNullOrWhiteSpace(correlationId.GetString()));
    }

    [TestMethod]
    public async Task ValidIncomingCorrelationIdIsReturned()
    {
        const string correlationId = "m1-api-test";

        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            new Uri("/", UriKind.Relative));
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        using var response = await client.SendAsync(request);

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
        CollectionAssert.Contains(
            response.Headers
                .GetValues(CorrelationIdMiddleware.HeaderName)
                .ToArray(),
            correlationId);
    }

    [TestMethod]
    public async Task OpenApiDocumentIncludesMetadataEndpoint()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            new Uri("/openapi/v1.json", UriKind.Relative));

        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStreamAsync());
        Assert.IsTrue(
            document.RootElement
                .GetProperty("paths")
                .TryGetProperty("/", out _));
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
