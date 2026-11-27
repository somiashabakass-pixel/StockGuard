using Microsoft.EntityFrameworkCore;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Tests.Integration;

[Trait("Category", "Integration")]
public class AuditIdempotencyIntegrationTests
{
    [Theory]
    [InlineData("SameRequest", 1)]
    [InlineData("ConflictingRequest", 1)]
    [InlineData("DifferentKeys", 2)]
    [InlineData("NoKey", 2)]
    [InlineData("ConcurrentRequests", 1)]
    [InlineData("ConcurrentConflicts", 1)]
    public async Task Idempotency_ShouldProtectAuditRecords(
        string scenario,
        int expectedAuditCount)
    {
        var options = TestDatabase.CreateOptions();

        await using var context =
            new StockGuardDbContext(options);

        Organization organization = new()
        {
            Name = "Idempotency Test Company"
        };

        Store store = new()
        {
            OrganizationId = organization.Id,
            Name = "Idempotency Test Store",
            ShopifyDomain = $"{Guid.NewGuid():N}.myshopify.com"
        };

        Warehouse warehouse = new()
        {
            OrganizationId = organization.Id,
            Name = "Idempotency Test Warehouse"
        };

        try
        {
            context.Organizations.Add(organization);
            context.Stores.Add(store);
            context.Warehouses.Add(warehouse);

            await context.SaveChangesAsync();

            var interceptor =
                new ConcurrentAuditSaveInterceptor();

            bool controlledConcurrency =
                scenario == "ConcurrentRequests" ||
                scenario == "ConcurrentConflicts";

            var persistenceOptions = controlledConcurrency
                ? new DbContextOptionsBuilder<StockGuardDbContext>(
                    options)
                    .AddInterceptors(interceptor)
                    .Options
                : options;

            AuditPersistenceService persistence =
                new(persistenceOptions);

            string key = Guid.NewGuid().ToString("N");

            async Task<AuditSaveResult> SaveAsync(
                string? requestKey,
                int warehouseQuantity = 90)
            {
                List<InventoryRecord> shopify = new()
                {
                    new()
                    {
                        SKU = "TEST-001",
                        Quantity = 100
                    }
                };

                List<InventoryRecord> warehouseInventory = new()
                {
                    new()
                    {
                        SKU = "TEST-001",
                        Quantity = warehouseQuantity
                    }
                };

                var results = new InventoryAuditService()
                    .CompareInventory(
                        shopify,
                        warehouseInventory);

                var risk = new InventoryRiskService()
                    .CalculateRisk(results);

                string? hash = requestKey is null
                    ? null
                    : AuditRequestHashService.ComputeHash(
                        organization.Id,
                        store.Id,
                        warehouse.Id,
                        shopify,
                        warehouseInventory);

                return await persistence.SaveAuditWithDetailsAsync(
                    organization.Id,
                    store.Id,
                    warehouse.Id,
                    DateTime.UtcNow,
                    results,
                    risk,
                    idempotencyKey: requestKey,
                    requestHash: hash);
            }

            switch (scenario)
            {
                case "SameRequest":
                {
                    var first = await SaveAsync(key);
                    var second = await SaveAsync(key);

                    Assert.Equal(
                        first.AuditId,
                        second.AuditId);

                    Assert.Equal(
                        first.CompletedAtUtc,
                        second.CompletedAtUtc);

                    break;
                }

                case "ConflictingRequest":
                {
                    await SaveAsync(key);

                    await Assert.ThrowsAsync<
                        IdempotencyConflictException>(
                        () => SaveAsync(key, 80));

                    break;
                }

                case "DifferentKeys":
                {
                    var first = await SaveAsync(key);

                    var second = await SaveAsync(
                        Guid.NewGuid().ToString("N"));

                    Assert.NotEqual(
                        first.AuditId,
                        second.AuditId);

                    break;
                }

                case "NoKey":
                {
                    var first = await SaveAsync(null);
                    var second = await SaveAsync(null);

                    Assert.NotEqual(
                        first.AuditId,
                        second.AuditId);

                    break;
                }

                case "ConcurrentRequests":
                {
                    var tasks = Enumerable.Range(0, 10)
                        .Select(_ => SaveAsync(key))
                        .ToArray();

                    var saved = await Task.WhenAll(tasks);

                    Assert.Single(
                        saved.Select(x => x.AuditId)
                            .Distinct());

                    Assert.Single(
                        saved.Select(x => x.CompletedAtUtc)
                            .Distinct());

                    break;
                }

                case "ConcurrentConflicts":
                {
                    async Task<AuditSaveResult?> AttemptAsync(
                        int quantity)
                    {
                        try
                        {
                            return await SaveAsync(key, quantity);
                        }
                        catch (IdempotencyConflictException)
                        {
                            return null;
                        }
                    }

                    var attempts = await Task.WhenAll(
                        AttemptAsync(90),
                        AttemptAsync(80));

                    Assert.Single(
                        attempts, x => x is not null);

                    Assert.Single(
                        attempts, x => x is null);

                    break;
                }

                default:
                    throw new InvalidOperationException(
                        "Unknown test scenario.");
            }

            if (controlledConcurrency)
            {
                int expectedArrivals =
                    scenario == "ConcurrentConflicts" ? 2 : 10;

                Assert.InRange(
                    interceptor.Arrivals,
                    2,
                    expectedArrivals);
            }

            await using var verification =
                new StockGuardDbContext(options);

            var audits = await verification.AuditRuns
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organization.Id)
                .ToListAsync();

            Assert.Equal(
                expectedAuditCount,
                audits.Count);

            var auditIds = audits
                .Select(x => x.Id)
                .ToList();

            int resultCount =
                await verification.AuditResults
                    .CountAsync(x =>
                        auditIds.Contains(x.AuditRunId));

            Assert.Equal(
                expectedAuditCount,
                resultCount);

            Assert.All(audits, audit =>
            {
                Assert.Equal("Completed", audit.Status);
                Assert.Equal(1, audit.TotalProducts);
                if (scenario == "ConcurrentConflicts")
                {
                    Assert.Contains(
                        audit.TotalUnitsDiscrepancy,
                        new[] { 10, 20 });
                }
                else
                {
                    Assert.Equal(
                        10,
                        audit.TotalUnitsDiscrepancy);
                }

                if (scenario == "NoKey")
                {
                    Assert.Null(audit.IdempotencyKey);
                    Assert.Null(audit.RequestHash);
                }
                else
                {
                    Assert.False(
                        string.IsNullOrWhiteSpace(
                            audit.IdempotencyKey));

                    Assert.Matches(
                        "^[0-9A-F]{64}$",
                        audit.RequestHash!);
                }
            });
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
