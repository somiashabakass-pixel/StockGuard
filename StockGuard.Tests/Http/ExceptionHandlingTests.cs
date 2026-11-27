using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using Xunit;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class ExceptionHandlingTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public ExceptionHandlingTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
        _factory.VerifyDatabaseIsolation();
    }

    [Theory]
    [InlineData(413)]
    [InlineData(400)]
    [InlineData(500)]
    public async Task Exception_ShouldReturnCorrectProblemDetails(
        int expectedStatus)
    {
        using var application = _factory.WithWebHostBuilder(
            builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<InventoryAuditService>();

                    services.AddSingleton<InventoryAuditService>(
                        new ThrowingAuditService(expectedStatus));
                });
            });

        using var client = application.CreateClient();

        var request = new
        {
            shopifyInventory = new[]
            {
                new { sku = "EXCEPTION-001", quantity = 100 }
            },
            warehouseInventory = new[]
            {
                new { sku = "EXCEPTION-001", quantity = 90 }
            }
        };

        using var response = await client.PostAsJsonAsync(
            "/api/audits/compare", request);

        Assert.Equal(
            (HttpStatusCode)expectedStatus,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        string body =
            await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(body);

        Assert.Equal(
            expectedStatus,
            document.RootElement
                .GetProperty("status")
                .GetInt32());

        Assert.DoesNotContain(
            "INTERNAL-TEST-SECRET", body);

        using var health =
            await client.GetAsync("/api/health");

        Assert.Equal(
            HttpStatusCode.OK,
            health.StatusCode);
    }

    private sealed class ThrowingAuditService
        : InventoryAuditService
    {
        private readonly int _statusCode;

        public ThrowingAuditService(int statusCode)
        {
            _statusCode = statusCode;
        }

        public override List<AuditResult> CompareInventory(
            List<InventoryRecord> shopifyInventory,
            List<InventoryRecord> warehouseInventory,
            CancellationToken cancellationToken = default)
        {
            if (_statusCode == 500)
            {
                throw new InvalidOperationException(
                    "INTERNAL-TEST-SECRET");
            }

            throw new BadHttpRequestException(
                "INTERNAL-TEST-SECRET",
                _statusCode);
        }
    }
}