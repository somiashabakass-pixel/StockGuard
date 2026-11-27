using Microsoft.EntityFrameworkCore;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Tests.Integration;

[Trait("Category", "Integration")]
public class EarlyIdempotencyLookupTests
{
    [Theory]
    [InlineData("ExistingRequest")]
    [InlineData("UnknownKey")]
    [InlineData("DifferentHash")]
    [InlineData("DifferentStore")]
    [InlineData("DifferentWarehouse")]
    [InlineData("DifferentOrganization")]
    [InlineData("CancelledRequest")]
    [InlineData("InvalidKey")]
    [InlineData("InvalidHash")]
    public async Task EarlyLookup_ShouldEnforceSafety(
        string scenario)
    {
        var options = TestDatabase.CreateOptions();

        await using var context =
            new StockGuardDbContext(options);

        Organization organization = new()
        {
            Name = "Early Lookup Test Company"
        };

        Store store = new()
        {
            OrganizationId = organization.Id,
            Name = "Early Lookup Store",
            ShopifyDomain =
                $"{Guid.NewGuid():N}.myshopify.com"
        };

        Warehouse warehouse = new()
        {
            OrganizationId = organization.Id,
            Name = "Early Lookup Warehouse"
        };

        try
        {
            context.Organizations.Add(organization);
            context.Stores.Add(store);
            context.Warehouses.Add(warehouse);

            await context.SaveChangesAsync();

            var persistence =
                new AuditPersistenceService(options);

            string key = Guid.NewGuid().ToString("N");

            List<InventoryRecord> shopify = new()
            {
                new()
                {
                    SKU = "EARLY-001",
                    Quantity = 100
                }
            };

            List<InventoryRecord> warehouseInventory = new()
            {
                new()
                {
                    SKU = "EARLY-001",
                    Quantity = 90
                }
            };

            string hash =
                AuditRequestHashService.ComputeHash(
                    organization.Id,
                    store.Id,
                    warehouse.Id,
                    shopify,
                    warehouseInventory);

            var results = new InventoryAuditService()
                .CompareInventory(
                    shopify,
                    warehouseInventory);

            var risk = new InventoryRiskService()
                .CalculateRisk(results);

            var saved =
                await persistence.SaveAuditWithDetailsAsync(
                    organization.Id,
                    store.Id,
                    warehouse.Id,
                    DateTime.UtcNow,
                    results,
                    risk,
                    idempotencyKey: key,
                    requestHash: hash);

            Task<PersistedAuditSnapshot?> LookupAsync(
                Guid? organizationId = null,
                Guid? storeId = null,
                Guid? warehouseId = null,
                string? requestKey = null,
                string? requestHash = null,
                CancellationToken cancellationToken = default)
            {
                return persistence.TryGetExistingAuditAsync(
                    organizationId ?? organization.Id,
                    storeId ?? store.Id,
                    warehouseId ?? warehouse.Id,
                    requestKey ?? key,
                    requestHash ?? hash,
                    cancellationToken);
            }

            switch (scenario)
            {
                case "ExistingRequest":
                {
                    var snapshot = await LookupAsync();

                    Assert.NotNull(snapshot);
                    Assert.Equal(
                        saved.AuditId,
                        snapshot.AuditId);

                    Assert.Equal(
                        saved.CompletedAtUtc,
                        snapshot.CompletedAtUtc);

                    Assert.Equal(1, snapshot.TotalProducts);
                    Assert.Equal(1, snapshot.MismatchedProducts);
                    Assert.Equal(10, snapshot.TotalUnitsDiscrepancy);

                    var result = Assert.Single(snapshot.Results);

                    Assert.Equal("EARLY-001", result.SKU);
                    Assert.Equal(100, result.ShopifyQuantity);
                    Assert.Equal(90, result.WarehouseQuantity);
                    Assert.Equal(10, result.Difference);
                    Assert.Equal("Mismatch", result.Status);
                    Assert.Equal("Medium", result.Severity);

                    break;
                }

                case "UnknownKey":
                {
                    Assert.Null(await LookupAsync(
                        requestKey: Guid.NewGuid().ToString("N")));

                    break;
                }

                case "DifferentHash":
                {
                    string differentHash =
                        (hash[0] == 'A' ? "B" : "A") +
                        hash.Substring(1);

                    await Assert.ThrowsAsync<
                        IdempotencyConflictException>(
                        () => LookupAsync(
                            requestHash: differentHash));

                    break;
                }

                case "DifferentStore":
                {
                    await Assert.ThrowsAsync<
                        IdempotencyConflictException>(
                        () => LookupAsync(
                            storeId: Guid.NewGuid()));

                    break;
                }

                case "DifferentWarehouse":
                {
                    await Assert.ThrowsAsync<
                        IdempotencyConflictException>(
                        () => LookupAsync(
                            warehouseId: Guid.NewGuid()));

                    break;
                }

                case "DifferentOrganization":
                {
                    Assert.Null(await LookupAsync(
                        organizationId: Guid.NewGuid()));

                    break;
                }

                case "CancelledRequest":
                {
                    using var cancellation = new CancellationTokenSource();

                    cancellation.Cancel();

                    await Assert.ThrowsAnyAsync<
                        OperationCanceledException>(
                        () => LookupAsync(
                            cancellationToken: cancellation.Token));

                    break;
                }

                case "InvalidKey":
                {
                    await Assert.ThrowsAsync<ArgumentException>(
                        () => LookupAsync(
                            requestKey: new string('A', 129)));

                    break;
                }

                case "InvalidHash":
                {
                    await Assert.ThrowsAsync<ArgumentException>(
                        () => LookupAsync(
                            requestHash: "invalid-hash"));

                    break;
                }

                default:
                    throw new InvalidOperationException(
                        "Unknown test scenario.");
            }

            await using var verification =
                new StockGuardDbContext(options);

            var audit = Assert.Single(
                await verification.AuditRuns
                    .AsNoTracking()
                    .Where(x =>
                        x.OrganizationId == organization.Id)
                    .ToListAsync());

            Assert.Equal(saved.AuditId, audit.Id);

            Assert.Equal(
                1,
                await verification.AuditResults
                    .CountAsync(x =>
                        x.AuditRunId == saved.AuditId));
        }
        finally
        {
            await using var cleanup =
                new StockGuardDbContext(options);

            await cleanup.AuditRuns
                .Where(x =>
                    x.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();

            await cleanup.Stores
                .Where(x =>
                    x.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();

            await cleanup.Warehouses
                .Where(x =>
                    x.OrganizationId == organization.Id)
                .ExecuteDeleteAsync();

            await cleanup.Organizations
                .Where(x => x.Id == organization.Id)
                .ExecuteDeleteAsync();
        }
    }
}