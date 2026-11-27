using Microsoft.EntityFrameworkCore;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;
using Xunit;

namespace StockGuard.Tests.Integration;

[Trait("Category", "Integration")]
public class DatabaseCancellationTests
{
    [Fact]
    public async Task GetAuditHistory_CancelledToken_Throws()
    {
        var options = TestDatabase.CreateOptions();

        AuditHistoryService service = new(options);

        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetAuditHistoryAsync(
                Guid.NewGuid(), 50, source.Token));
    }

    [Fact]
    public async Task GetAuditResults_CancelledToken_Throws()
    {
        var options = TestDatabase.CreateOptions();

        AuditHistoryService service = new(options);

        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.GetAuditResultsAsync(
                Guid.NewGuid(),
                Guid.NewGuid(),
                source.Token));
    }

    [Fact]
    public async Task CancelledAudit_ShouldNotPersistRecords()
    {
        var options = TestDatabase.CreateOptions();

        await using var context =
            new StockGuardDbContext(options);

        Organization organization = new()
        {
            Name = "Cancellation Test Company"
        };

        Store store = new()
        {
            OrganizationId = organization.Id,
            Name = "Cancellation Test Store",
            ShopifyDomain =
                $"cancel-{Guid.NewGuid():N}.myshopify.com"
        };

        Warehouse warehouse = new()
        {
            OrganizationId = organization.Id,
            Name = "Cancellation Test Warehouse"
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
                    SKU = "CANCEL-001",
                    ShopifyQuantity = 100,
                    WarehouseQuantity = 90,
                    Difference = 10,
                    Status = "Mismatch",
                    Severity = "Medium"
                }
            };

            InventoryRiskService riskService = new();

            var risk = riskService.CalculateRisk(results);

            AuditPersistenceService persistence =
                new(options);

            using var source = new CancellationTokenSource();
            source.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => persistence.SaveAuditWithDetailsAsync(
                    organization.Id,
                    store.Id,
                    warehouse.Id,
                    DateTime.UtcNow,
                    results,
                    risk,
                    source.Token));

            await using var verification =
                new StockGuardDbContext(options);

            Assert.False(
                await verification.AuditRuns.AnyAsync(
                    x => x.OrganizationId == organization.Id));

            Assert.False(
                await verification.AuditResults.AnyAsync(
                    x => x.AuditRun.OrganizationId ==
                        organization.Id));

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
