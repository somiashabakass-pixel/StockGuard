using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data;
using StockGuard.Tests.Integration;
using Xunit;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class AuditWriteSafetyTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public AuditWriteSafetyTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
        _factory.VerifyDatabaseIsolation();
    }

    [Theory]
    [InlineData("compare", 90, 200)]
    [InlineData("save", -10, 400)]
    [InlineData("compare", -10, 400)]
    public async Task Request_ShouldNotPersistAudit(
        string endpoint,
        int warehouseQuantity,
        int expectedStatus)
    {
        await HttpTestData.InitializeAsync();

        string sku =
            $"SAFETY-{Guid.NewGuid():N}".ToUpperInvariant();

        try
        {
            using var client = _factory.CreateClient();

            var request = new
            {
                shopifyInventory = new[]
                {
                    new { sku, quantity = 100 }
                },
                warehouseInventory = new[]
                {
                    new
                    {
                        sku,
                        quantity = warehouseQuantity
                    }
                }
            };

            using var response = await client.PostAsJsonAsync(
                $"/api/audits/{endpoint}", request);

            Assert.Equal(
                (HttpStatusCode)expectedStatus,
                response.StatusCode);

            if (expectedStatus == 200)
            {
                using var document = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync());

                var root = document.RootElement;

                Assert.Equal(
                    JsonValueKind.Null,
                    root.GetProperty("auditId").ValueKind);

                Assert.Equal(
                    10,
                    root.GetProperty("totalUnitsDiscrepancy")
                        .GetInt32());
            }

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
        finally
        {
            await using var cleanup =
                TestDatabase.CreateContext();

            var ownedAuditIds = await cleanup.AuditResults
                .Where(x =>
                    x.SKU == sku &&
                    x.AuditRun.OrganizationId ==
                        DevelopmentDataService.OrganizationId)
                .Select(x => x.AuditRunId)
                .Distinct()
                .ToListAsync();

            await cleanup.AuditRuns
                .Where(x =>
                    ownedAuditIds.Contains(x.Id) &&
                    x.OrganizationId ==
                        DevelopmentDataService.OrganizationId)
                .ExecuteDeleteAsync();
        }
    }
}
