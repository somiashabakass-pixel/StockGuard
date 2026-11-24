using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;
using Xunit;

namespace StockGuard.Tests.Integration;

[Trait("Category", "Integration")]
public class TransactionIntegrityIntegrationTests
{
    [Fact]
    public async Task FailedAudit_ShouldRollbackEntireTransaction()
    {
        var options = TestDatabase.CreateOptions();

        await using var context =
            new StockGuardDbContext(options);

        Organization organization = new()
        {
            Name = "Transaction Test Company"
        };

        Store store = new()
        {
            OrganizationId = organization.Id,
            Name = "Transaction Test Store",
            ShopifyDomain = "transaction-test.myshopify.com"
        };

        Warehouse warehouse = new()
        {
            OrganizationId = organization.Id,
            Name = "Transaction Test Warehouse"
        };

        try
        {
            context.Organizations.Add(organization);
            context.Stores.Add(store);
            context.Warehouses.Add(warehouse);

            await context.SaveChangesAsync();

            List<AuditResult> results = new()
            {
                new()
                {
                    SKU = "DUPLICATE-001",
                    ShopifyQuantity = 100,
                    WarehouseQuantity = 90,
                    Difference = 10,
                    Status = "Mismatch",
                    Severity = "Medium"
                },
                new()
                {
                    SKU = "DUPLICATE-001",
                    ShopifyQuantity = 50,
                    WarehouseQuantity = 45,
                    Difference = 5,
                    Status = "Mismatch",
                    Severity = "Low"
                }
            };

            InventoryRiskService riskService = new();

            var riskSummary =
                riskService.CalculateRisk(results);

            AuditPersistenceService persistence =
                new(options);

            var exception =
                await Assert.ThrowsAsync<DbUpdateException>(
                    () => persistence.SaveAuditAsync(
                        organization.Id,
                        store.Id,
                        warehouse.Id,
                        DateTime.UtcNow,
                        results,
                        riskSummary));

            var postgresException =
                Assert.IsType<PostgresException>(
                    exception.InnerException);

            Assert.Equal(
                PostgresErrorCodes.UniqueViolation,
                postgresException.SqlState);

            await using var verification =
                new StockGuardDbContext(options);

            int auditCount =
                await verification.AuditRuns
                    .CountAsync(x =>
                        x.OrganizationId == organization.Id);

            int resultCount =
                await verification.AuditResults
                    .CountAsync(x =>
                        x.AuditRun.OrganizationId ==
                        organization.Id);

            Assert.Equal(0, auditCount);
            Assert.Equal(0, resultCount);

            Assert.True(
                await verification.Organizations.AnyAsync(
                    x => x.Id == organization.Id));

            Assert.True(
                await verification.Stores.AnyAsync(
                    x => x.Id == store.Id));

            Assert.True(
                await verification.Warehouses.AnyAsync(
                    x => x.Id == warehouse.Id));
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
