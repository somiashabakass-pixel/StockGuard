using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Infrastructure.Data;

public class AuditHistoryService
{
    private readonly DbContextOptions<StockGuardDbContext> _options;

    public AuditHistoryService()
        : this(CreateDefaultOptions())
    {
    }

    public AuditHistoryService(
        DbContextOptions<StockGuardDbContext> options)
    {
        _options = options;
    }

    private static DbContextOptions<StockGuardDbContext>
        CreateDefaultOptions()
    {
        string connectionString =
            Environment.GetEnvironmentVariable(
                "STOCKGUARD_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Database connection is not configured.");

        return new DbContextOptionsBuilder<StockGuardDbContext>()
            .UseNpgsql(connectionString)
            .Options;
    }

    public async Task<List<AuditRun>> GetAuditHistoryAsync(
        Guid organizationId,
        int limit = 50)
    {
        if (limit < 1 || limit > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using var context =
            new StockGuardDbContext(_options);

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
        await using var context =
            new StockGuardDbContext(_options);

        return await context.AuditResults
            .AsNoTracking()
            .Where(x =>
                x.AuditRunId == auditId &&
                x.AuditRun.OrganizationId == organizationId)
            .OrderBy(x => x.SKU)
            .ToListAsync();
    }
}
