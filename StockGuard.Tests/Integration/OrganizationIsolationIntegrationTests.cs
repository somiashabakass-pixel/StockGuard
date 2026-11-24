using Microsoft.EntityFrameworkCore;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;
using Xunit;

namespace StockGuard.Tests.Integration;

[Trait("Category", "Integration")]
public class OrganizationIsolationIntegrationTests
{
    [Fact]
    public async Task Organizations_ShouldNotAccessEachOthersAudits()
    {
        var options = TestDatabase.CreateOptions();

        await using var context =
            new StockGuardDbContext(options);

        Organization companyA = new()
        {
            Name = "Test Company A"
        };

        Organization companyB = new()
        {
            Name = "Test Company B"
        };

        Store storeA = new()
        {
            OrganizationId = companyA.Id,
            Name = "Store A",
            ShopifyDomain = "test-a.myshopify.com"
        };

        Store storeB = new()
        {
            OrganizationId = companyB.Id,
            Name = "Store B",
            ShopifyDomain = "test-b.myshopify.com"
        };

        Warehouse warehouseA = new()
        {
            OrganizationId = companyA.Id,
            Name = "Warehouse A"
        };

        Warehouse warehouseB = new()
        {
            OrganizationId = companyB.Id,
            Name = "Warehouse B"
        };

        try
        {
            context.Organizations.AddRange(
                companyA, companyB);

            context.Stores.AddRange(
                storeA, storeB);

            context.Warehouses.AddRange(
                warehouseA, warehouseB);

            await context.SaveChangesAsync();

            List<AuditResult> results = new()
            {
                new()
                {
                    SKU = "TEST-001",
                    ShopifyQuantity = 100,
                    WarehouseQuantity = 90,
                    Difference = 10,
                    Status = "Mismatch",
                    Severity = "Medium"
                }
            };

            InventoryRiskService riskService = new();

            var riskSummary =
                riskService.CalculateRisk(results);

            AuditPersistenceService persistence =
                new(options);

            Guid auditA = await persistence.SaveAuditAsync(
                companyA.Id,
                storeA.Id,
                warehouseA.Id,
                DateTime.UtcNow,
                results,
                riskSummary);

            Guid auditB = await persistence.SaveAuditAsync(
                companyB.Id,
                storeB.Id,
                warehouseB.Id,
                DateTime.UtcNow,
                results,
                riskSummary);

            AuditHistoryService history = new(options);

            var historyA =
                await history.GetAuditHistoryAsync(companyA.Id);

            var historyB =
                await history.GetAuditHistoryAsync(companyB.Id);

            Assert.Equal(auditA, Assert.Single(historyA).Id);
            Assert.Equal(auditB, Assert.Single(historyB).Id);

            var ownResultsA =
                await history.GetAuditResultsAsync(
                    companyA.Id, auditA);

            var ownResultsB =
                await history.GetAuditResultsAsync(
                    companyB.Id, auditB);

            Assert.Single(ownResultsA);
            Assert.Single(ownResultsB);

            var unauthorizedA =
                await history.GetAuditResultsAsync(
                    companyA.Id, auditB);

            var unauthorizedB =
                await history.GetAuditResultsAsync(
                    companyB.Id, auditA);

            Assert.Empty(unauthorizedA);
            Assert.Empty(unauthorizedB);

            await Assert.ThrowsAsync<DbUpdateException>(
                () => persistence.SaveAuditAsync(
                    companyA.Id,
                    storeB.Id,
                    warehouseA.Id,
                    DateTime.UtcNow,
                    results,
                    riskSummary));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => persistence.SaveAuditAsync(
                    companyA.Id,
                    storeA.Id,
                    warehouseB.Id,
                    DateTime.UtcNow,
                    results,
                    riskSummary));

            Assert.Single(
                await history.GetAuditHistoryAsync(companyA.Id));

            Assert.Single(
                await history.GetAuditHistoryAsync(companyB.Id));
        }
        finally
        {
            await using var cleanup =
                new StockGuardDbContext(options);

            Guid[] organizationIds =
            {
                companyA.Id,
                companyB.Id
            };

            await cleanup.AuditRuns
                .Where(x => organizationIds.Contains(
                    x.OrganizationId))
                .ExecuteDeleteAsync();

            await cleanup.Stores
                .Where(x => organizationIds.Contains(
                    x.OrganizationId))
                .ExecuteDeleteAsync();

            await cleanup.Warehouses
                .Where(x => organizationIds.Contains(
                    x.OrganizationId))
                .ExecuteDeleteAsync();

            await cleanup.Organizations
                .Where(x => organizationIds.Contains(x.Id))
                .ExecuteDeleteAsync();
        }
    }
}
