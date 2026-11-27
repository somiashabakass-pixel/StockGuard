using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data;
using StockGuard.Tests.Integration;
using Xunit;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class JsonPersistenceEndpointTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public JsonPersistenceEndpointTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
        _factory.VerifyDatabaseIsolation();
    }

    [Fact]
    public async Task Save_ValidInventory_PersistsAuditAndHistory()
    {
        await HttpTestData.InitializeAsync();

        string prefix =
            $"HTTP-JSON-{Guid.NewGuid():N}".ToUpperInvariant();

        try
        {
            using var client = _factory.CreateClient();

            var request = new
            {
                shopifyInventory = new[]
                {
                    new { sku = $"{prefix}-001", quantity = 100 },
                    new { sku = $"{prefix}-002", quantity = 50 },
                    new { sku = $"{prefix}-003", quantity = 75 }
                },
                warehouseInventory = new[]
                {
                    new { sku = $"{prefix}-001", quantity = 92 },
                    new { sku = $"{prefix}-002", quantity = 50 },
                    new { sku = $"{prefix}-003", quantity = 60 },
                    new { sku = $"{prefix}-004", quantity = 40 }
                }
            };

            using var response = await client.PostAsJsonAsync(
                "/api/audits/save", request);

            string body =
                await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                body);

            using var document = JsonDocument.Parse(body);

            var root = document.RootElement;

            Guid auditId =
                root.GetProperty("auditId").GetGuid();

            Assert.NotEqual(Guid.Empty, auditId);

            DateTime completedAtUtc =
                root.GetProperty("completedAtUtc").GetDateTime();

            Assert.Equal(4,
                root.GetProperty("totalProducts").GetInt32());

            Assert.Equal(1,
                root.GetProperty("matchingProducts").GetInt32());

            Assert.Equal(2,
                root.GetProperty("mismatchedProducts").GetInt32());

            Assert.Equal(1,
                root.GetProperty("missingProducts").GetInt32());

            Assert.Equal(23,
                root.GetProperty("totalUnitsDiscrepancy").GetInt32());

            await using var context =
                TestDatabase.CreateContext();

            var savedAudit = await context.AuditRuns
                .AsNoTracking()
                .SingleAsync(x => x.Id == auditId);

            Assert.Equal(
                DevelopmentDataService.OrganizationId,
                savedAudit.OrganizationId);

            Assert.Equal(
                DevelopmentDataService.StoreId,
                savedAudit.StoreId);

            Assert.Equal(
                DevelopmentDataService.WarehouseId,
                savedAudit.WarehouseId);

            Assert.Equal("Completed", savedAudit.Status);
            Assert.Equal(4, savedAudit.TotalProducts);
            Assert.Equal(1, savedAudit.MatchingProducts);
            Assert.Equal(2, savedAudit.MismatchedProducts);
            Assert.Equal(1, savedAudit.MissingProducts);
            Assert.Equal(23, savedAudit.TotalUnitsDiscrepancy);

            Assert.Equal(
                completedAtUtc,
                savedAudit.CompletedAtUtc!.Value);

            Assert.Equal(
                DateTimeKind.Utc,
                savedAudit.CompletedAtUtc.Value.Kind);

            Assert.True(
                savedAudit.CompletedAtUtc.Value >=
                savedAudit.StartedAtUtc);

            Assert.Equal(
                0L,
                savedAudit.CompletedAtUtc.Value.Ticks % 10);

            using var historyResponse = await client.GetAsync(
                "/api/audits/history?limit=100");

            Assert.Equal(
                HttpStatusCode.OK,
                historyResponse.StatusCode);

            using var historyDocument = JsonDocument.Parse(
                await historyResponse.Content.ReadAsStringAsync());

            var historyAudit = Assert.Single(
                historyDocument.RootElement.EnumerateArray(),
                x => x.GetProperty("auditId").GetGuid() == auditId);

            Assert.Equal(
                completedAtUtc,
                historyAudit.GetProperty("completedAtUtc")
                    .GetDateTime());

            Assert.Equal(
                4,
                historyAudit.GetProperty("totalProducts")
                    .GetInt32());

            Assert.Equal(
                23,
                historyAudit.GetProperty("totalUnitsDiscrepancy")
                    .GetInt32());

            using var resultsResponse = await client.GetAsync(
                $"/api/audits/history/{auditId}/results");

            Assert.Equal(
                HttpStatusCode.OK,
                resultsResponse.StatusCode);

            using var resultsDocument = JsonDocument.Parse(
                await resultsResponse.Content.ReadAsStringAsync());

            var results = resultsDocument.RootElement;

            Assert.Equal(4, results.GetArrayLength());

            var mismatch = Assert.Single(
                results.EnumerateArray(),
                x => x.GetProperty("sku").GetString() ==
                    $"{prefix}-001");

            Assert.Equal(
                100,
                mismatch.GetProperty("shopifyQuantity").GetInt32());

            Assert.Equal(
                92,
                mismatch.GetProperty("warehouseQuantity").GetInt32());

            Assert.Equal(
                8,
                mismatch.GetProperty("difference").GetInt32());

            Assert.Equal(
                "Mismatch",
                mismatch.GetProperty("status").GetString());

            Assert.Equal(
                "Medium",
                mismatch.GetProperty("severity").GetString());

            var databaseResults = await context.AuditResults
                .AsNoTracking()
                .Where(x => x.AuditRunId == auditId)
                .ToListAsync();

            Assert.Equal(4, databaseResults.Count);

            foreach (var item in databaseResults)
            {
                var apiItem = Assert.Single(
                    results.EnumerateArray(),
                    x => x.GetProperty("sku").GetString() ==
                        item.SKU);

                Assert.Equal(
                    item.Status,
                    apiItem.GetProperty("status").GetString());

                Assert.Equal(
                    item.Severity,
                    apiItem.GetProperty("severity").GetString());
            }
        }
        finally
        {
            await using var cleanup =
                TestDatabase.CreateContext();

            var ownedAuditIds = await cleanup.AuditResults
                .Where(x =>
                    x.SKU.StartsWith(prefix + "-") &&
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task History_InvalidLimit_ReturnsBadRequest(
        int limit)
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/audits/history?limit={limit}");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Results_UnknownAudit_ReturnsNotFound()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/audits/history/{Guid.NewGuid()}/results");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}
