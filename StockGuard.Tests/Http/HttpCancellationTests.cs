using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data;
using StockGuard.Tests.Integration;
using Xunit;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class HttpCancellationTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public HttpCancellationTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
        _factory.VerifyDatabaseIsolation();
    }

    [Theory]
    [InlineData("compare")]
    [InlineData("save")]
    public async Task CancelledRequest_ShouldNotPersistResults(
        string endpoint)
    {
        await HttpTestData.InitializeAsync();

        string sku =
            $"CANCEL-{Guid.NewGuid():N}".ToUpperInvariant();

        using var client = _factory.CreateClient();

        using var cancellation =
            new CancellationTokenSource();

        var request = new
        {
            shopifyInventory = new[]
            {
                new { sku, quantity = 100 }
            },
            warehouseInventory = new[]
            {
                new { sku, quantity = 90 }
            }
        };

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
            {
                using var response =
                    await client.PostAsJsonAsync(
                        $"/api/audits/{endpoint}",
                        request,
                        cancellation.Token);
            });

        await using var context =
            TestDatabase.CreateContext();

        bool exists = await context.AuditResults
            .AsNoTracking()
            .AnyAsync(x =>
                x.SKU == sku &&
                x.AuditRun.OrganizationId ==
                    DevelopmentDataService.OrganizationId);

        Assert.False(exists);
    }

    [Fact]
    public async Task CancelledHistoryRequest_ThrowsCancellation()
    {
        using var client = _factory.CreateClient();

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
            {
                using var response = await client.GetAsync(
                    "/api/audits/history",
                    cancellation.Token);
            });
    }

    [Fact]
    public async Task CancelledResultsRequest_ThrowsCancellation()
    {
        using var client = _factory.CreateClient();

        using var cancellation =
            new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () =>
            {
                using var response = await client.GetAsync(
                    $"/api/audits/history/{Guid.NewGuid()}/results",
                    cancellation.Token);
            });
    }
}
