using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Infrastructure.Data;

public class AuditHistoryService
{
    public async Task<List<AuditRun>> GetAuditHistoryAsync(
        Guid organizationId,
        int limit = 50)
    {
        if (limit < 1 || limit > 100)
        {
            throw new ArgumentOutOfRangeException(
                nameof(limit));
        }

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

        return await context.AuditRuns
            .AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .OrderByDescending(x => x.StartedAtUtc)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<List<AuditResultEntity>> GetAuditResultsAsync(
        Guid organizationId,
        Guid auditId)
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

        return await context.AuditResults
            .AsNoTracking()
            .Where(x =>
                x.AuditRunId == auditId &&
                x.AuditRun.OrganizationId == organizationId)
            .OrderBy(x => x.SKU)
            .ToListAsync();
    }
}
