using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class HealthEndpointTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public HealthEndpointTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
        _factory.VerifyDatabaseIsolation();
    }

    [Fact]
    public void TestServer_ShouldUseTestingEnvironment()
    {
        var environment = _factory.Services
            .GetRequiredService<IWebHostEnvironment>();

        Assert.Equal(
            "Testing",
            environment.EnvironmentName);
    }

    [Fact]
    public void TestServer_ShouldUseIsolatedDatabase()
    {
        _factory.VerifyDatabaseIsolation();
    }

    [Fact]
    public async Task HealthEndpoint_ShouldReturnHealthy()
    {
        using var client = _factory.CreateClient();

        using var response =
            await client.GetAsync("/api/health");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;

        Assert.Equal(
            "StockGuard Audit",
            root.GetProperty("application").GetString());

        Assert.Equal(
            "Healthy",
            root.GetProperty("status").GetString());
    }

    [Fact]
    public async Task DatabaseHealth_ShouldReturnConnected()
    {
        using var client = _factory.CreateClient();

        using var response =
            await client.GetAsync("/api/health/database");

        Assert.Equal(
            HttpStatusCode.OK,
            response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;

        Assert.Equal(
            "Healthy",
            root.GetProperty("status").GetString());

        Assert.Equal(
            "Connected",
            root.GetProperty("database").GetString());

        Assert.Equal(
            "PostgreSQL",
            root.GetProperty("provider").GetString());
    }
}
