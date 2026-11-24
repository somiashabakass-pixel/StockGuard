using Microsoft.EntityFrameworkCore;
using StockGuard.Core.Models;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Infrastructure.Data;

public class AuditPersistenceService
{
    public async Task<Guid> SaveAuditAsync(
        Guid organizationId,
        Guid storeId,
        Guid warehouseId,
        DateTime startedAtUtc,
        List<AuditResult> results,
        InventoryRiskSummary riskSummary)
    {
        string connectionString =
            Environment.GetEnvironmentVariable(
                "STOCKGUARD_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Database connection is not configured.");

        var options =
            new DbContextOptionsBuilder<StockGuardDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        await using var context =
            new StockGuardDbContext(options);

        AuditRun auditRun = new()
        {
            OrganizationId = organizationId,
            StoreId = storeId,
            WarehouseId = warehouseId,

            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = DateTime.UtcNow,

            Status = "Completed",

            TotalProducts = results.Count,

            MatchingProducts = results.Count(
                x => x.Status == "Match"),

            MismatchedProducts = results.Count(
                x => x.Status == "Mismatch"),

            MissingProducts = results.Count(
                x => x.Status.StartsWith("Missing")),

            TotalUnitsDiscrepancy =
                riskSummary.TotalUnitsDiscrepancy
        };

        context.AuditRuns.Add(auditRun);

        foreach (AuditResult result in results)
        {
            AuditResultEntity entity = new()
            {
                AuditRunId = auditRun.Id,

                SKU = result.SKU,

                ShopifyQuantity = result.ShopifyQuantity,

                WarehouseQuantity = result.WarehouseQuantity,

                Difference = result.Difference,

                Status = result.Status,

                Severity = result.Severity
            };

            context.AuditResults.Add(entity);
        }

        await context.SaveChangesAsync();

        return auditRun.Id;
    }
}
