using Microsoft.EntityFrameworkCore;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Tests.Integration;

[Trait("Category", "Integration")]
public class IdempotencyOrganizationIsolationTests
{
    [Fact]
    public async Task SameKey_DifferentOrganizations_ShouldCreateIndependentAudits()
    {
        var options = TestDatabase.CreateOptions();

        await using var context =
            new StockGuardDbContext(options);

        Organization organizationA = new()
        {
            Name = "Idempotency Company A"
        };

        Organization organizationB = new()
        {
            Name = "Idempotency Company B"
        };

        Store storeA = new()
        {
            OrganizationId = organizationA.Id,
            Name = "Store A",
            ShopifyDomain = $"{Guid.NewGuid():N}.myshopify.com"
        };

        Store storeB = new()
        {
            OrganizationId = organizationB.Id,
            Name = "Store B",
            ShopifyDomain = $"{Guid.NewGuid():N}.myshopify.com"
        };

        Warehouse warehouseA = new()
        {
            OrganizationId = organizationA.Id,
            Name = "Warehouse A"
        };

        Warehouse warehouseB = new()
        {
            OrganizationId = organizationB.Id,
            Name = "Warehouse B"
        };

        var organizationIds = new[]
        {
            organizationA.Id,
            organizationB.Id
        };

        try
        {
            context.Organizations.AddRange(
                organizationA, organizationB);

            context.Stores.AddRange(
                storeA, storeB);

            context.Warehouses.AddRange(
                warehouseA, warehouseB);

            await context.SaveChangesAsync();

            string sharedKey =
                Guid.NewGuid().ToString("N");

            List<InventoryRecord> shopify = new()
            {
                new() { SKU = "TEST-001", Quantity = 100 }
            };

            List<InventoryRecord> warehouse = new()
            {
                new() { SKU = "TEST-001", Quantity = 90 }
            };

            var results = new InventoryAuditService()
                .CompareInventory(shopify, warehouse);

            var risk = new InventoryRiskService()
                .CalculateRisk(results);

            var persistence =
                new AuditPersistenceService(options);

            async Task<AuditSaveResult> SaveAsync(
                Guid organizationId,
                Guid storeId,
                Guid warehouseId)
            {
                string hash =
                    AuditRequestHashService.ComputeHash(
                        organizationId,
                        storeId,
                        warehouseId,
                        shopify,
                        warehouse);

                return await persistence
                    .SaveAuditWithDetailsAsync(
                        organizationId,
                        storeId,
                        warehouseId,
                        DateTime.UtcNow,
                        results,
                        risk,
                        idempotencyKey: sharedKey,
                        requestHash: hash);
            }

            var first = await SaveAsync(
                organizationA.Id,
                storeA.Id,
                warehouseA.Id);

            var second = await SaveAsync(
                organizationB.Id,
                storeB.Id,
                warehouseB.Id);

            Assert.NotEqual(
                first.AuditId,
                second.AuditId);

            var retryA = await SaveAsync(
                organizationA.Id,
                storeA.Id,
                warehouseA.Id);

            var retryB = await SaveAsync(
                organizationB.Id,
                storeB.Id,
                warehouseB.Id);

            Assert.Equal(first, retryA);
            Assert.Equal(second, retryB);

            await using var verification =
                new StockGuardDbContext(options);

            var audits = await verification.AuditRuns
                .AsNoTracking()
                .Where(x =>
                    organizationIds.Contains(x.OrganizationId))
                .ToListAsync();

            Assert.Equal(2, audits.Count);

            var auditA = Assert.Single(
                audits,
                x => x.OrganizationId == organizationA.Id);

            var auditB = Assert.Single(
                audits,
                x => x.OrganizationId == organizationB.Id);

            Assert.Equal(first.AuditId, auditA.Id);
            Assert.Equal(second.AuditId, auditB.Id);

            Assert.Equal(sharedKey, auditA.IdempotencyKey);
            Assert.Equal(sharedKey, auditB.IdempotencyKey);

            Assert.NotEqual(
                auditA.RequestHash,
                auditB.RequestHash);

            var auditIds = audits.Select(x => x.Id).ToArray();

            Assert.Equal(
                2,
                await verification.AuditResults
                    .CountAsync(x =>
                        auditIds.Contains(x.AuditRunId)));
        }
        finally
        {
            await using var cleanup =
                new StockGuardDbContext(options);

            await cleanup.AuditRuns
                .Where(x =>
                    organizationIds.Contains(x.OrganizationId))
                .ExecuteDeleteAsync();

            await cleanup.Stores
                .Where(x =>
                    organizationIds.Contains(x.OrganizationId))
                .ExecuteDeleteAsync();

            await cleanup.Warehouses
                .Where(x =>
                    organizationIds.Contains(x.OrganizationId))
                .ExecuteDeleteAsync();

            await cleanup.Organizations
                .Where(x => organizationIds.Contains(x.Id))
                .ExecuteDeleteAsync();
        }
    }
}
