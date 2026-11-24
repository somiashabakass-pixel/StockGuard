using Microsoft.EntityFrameworkCore;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;
using Xunit;

namespace StockGuard.Tests.Integration;

[Trait("Category", "Integration")]
public class AuditPersistenceIntegrationTests
{
    [Fact]
    public async Task SaveAndRetrieveAudit_ShouldPreserveResults()
    {
        var options = TestDatabase.CreateOptions();

        await using var context =
            new StockGuardDbContext(options);

        Organization organization = new()
        {
            Name = "Integration Test Company"
        };

        Store store = new()
        {
            OrganizationId = organization.Id,
            Name = "Integration Test Store",
            ShopifyDomain = "integration-test.myshopify.com"
        };

        Warehouse warehouse = new()
        {
            OrganizationId = organization.Id,
            Name = "Integration Test Warehouse"
        };

        try
        {
            context.Organizations.Add(organization);
            context.Stores.Add(store);
            context.Warehouses.Add(warehouse);

            await context.SaveChangesAsync();

            List<InventoryRecord> shopifyInventory = new()
            {
                new() { SKU = "TEST-001", Quantity = 100 },
                new() { SKU = "TEST-002", Quantity = 50 },
                new() { SKU = "TEST-003", Quantity = 25 }
            };

            List<InventoryRecord> warehouseInventory = new()
            {
                new() { SKU = "TEST-001", Quantity = 90 },
                new() { SKU = "TEST-002", Quantity = 50 }
            };

            InventoryAuditService auditService = new();
            InventoryRiskService riskService = new();

            var results = auditService.CompareInventory(
                shopifyInventory,
                warehouseInventory);

            var riskSummary =
                riskService.CalculateRisk(results);

            AuditPersistenceService persistenceService =
                new(options);

            Guid auditId =
                await persistenceService.SaveAuditAsync(
                    organization.Id,
                    store.Id,
                    warehouse.Id,
                    DateTime.UtcNow,
                    results,
                    riskSummary);

            Assert.NotEqual(Guid.Empty, auditId);

            AuditHistoryService historyService =
                new(options);

            var history =
                await historyService.GetAuditHistoryAsync(
                    organization.Id);

            var savedAudit = Assert.Single(history);

            Assert.Equal(auditId, savedAudit.Id);
            Assert.Equal("Completed", savedAudit.Status);
            Assert.Equal(3, savedAudit.TotalProducts);
            Assert.Equal(1, savedAudit.MatchingProducts);
            Assert.Equal(1, savedAudit.MismatchedProducts);
            Assert.Equal(1, savedAudit.MissingProducts);
            Assert.Equal(10, savedAudit.TotalUnitsDiscrepancy);

            var savedResults =
                await historyService.GetAuditResultsAsync(
                    organization.Id,
                    auditId);

            Assert.Equal(3, savedResults.Count);

            var mismatch = Assert.Single(
                savedResults.Where(x => x.SKU == "TEST-001"));

            Assert.Equal(100, mismatch.ShopifyQuantity);
            Assert.Equal(90, mismatch.WarehouseQuantity);
            Assert.Equal(10, mismatch.Difference);
            Assert.Equal("Mismatch", mismatch.Status);
            Assert.Equal("Medium", mismatch.Severity);

            var match = Assert.Single(
                savedResults.Where(x => x.SKU == "TEST-002"));

            Assert.Equal("Match", match.Status);
            Assert.Equal(0, match.Difference);

            var missing = Assert.Single(
                savedResults.Where(x => x.SKU == "TEST-003"));

            Assert.Equal("Missing from Warehouse", missing.Status);
            Assert.Null(missing.WarehouseQuantity);
        }
        finally
        {
            await using var cleanup =
                new StockGuardDbContext(options);

            await cleanup.AuditRuns
                .Where(x => x.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();

            await cleanup.Stores
                .Where(x => x.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();

            await cleanup.Warehouses
                .Where(x => x.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();

            await cleanup.Organizations
                .Where(x => x.Id == organization.Id)
                .ExecuteDeleteAsync();
        }
    }
}
