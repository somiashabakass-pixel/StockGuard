using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockGuard.Core.Models;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Infrastructure.Data;

public class AuditPersistenceService
{
    private const string IdempotencyIndex =
        "UX_audit_runs_organization_idempotency";

    private readonly DbContextOptions<StockGuardDbContext> _options;

    public AuditPersistenceService()
        : this(CreateDefaultOptions())
    {
    }

    public AuditPersistenceService(
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

    public async Task<Guid> SaveAuditAsync(
        Guid organizationId,
        Guid storeId,
        Guid warehouseId,
        DateTime startedAtUtc,
        List<AuditResult> results,
        InventoryRiskSummary riskSummary,
        CancellationToken cancellationToken = default)
    {
        AuditSaveResult savedAudit =
            await SaveAuditWithDetailsAsync(
                organizationId,
                storeId,
                warehouseId,
                startedAtUtc,
                results,
                riskSummary,
                cancellationToken);

        return savedAudit.AuditId;
    }

    public async Task<AuditSaveResult> SaveAuditWithDetailsAsync(
        Guid organizationId,
        Guid storeId,
        Guid warehouseId,
        DateTime startedAtUtc,
        List<AuditResult> results,
        InventoryRiskSummary riskSummary,
        CancellationToken cancellationToken = default,
        string? idempotencyKey = null,
        string? requestHash = null)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ValidateIdempotency(idempotencyKey, requestHash);

        DateTime normalizedStartedAtUtc =
            NormalizeTimestamp(startedAtUtc);

        if (idempotencyKey is not null)
        {
            var existing = await FindExistingAuditAsync(
                organizationId,
                storeId,
                warehouseId,
                idempotencyKey,
                requestHash!,
                cancellationToken);

            if (existing is not null)
            {
                return existing;
            }
        }

        await using var context =
            new StockGuardDbContext(_options);

        DateTime completedAtUtc =
            NormalizeTimestamp(DateTime.UtcNow);

        AuditRun auditRun = new()
        {
            OrganizationId = organizationId,
            StoreId = storeId,
            WarehouseId = warehouseId,
            IdempotencyKey = idempotencyKey,
            RequestHash = requestHash,
            StartedAtUtc = normalizedStartedAtUtc,
            CompletedAtUtc = completedAtUtc,
            Status = "Completed",
            TotalProducts = results.Count,
            MatchingProducts =
                results.Count(x => x.Status == "Match"),
            MismatchedProducts =
                results.Count(x => x.Status == "Mismatch"),
            MissingProducts =
                results.Count(x => x.Status.StartsWith("Missing")),
            TotalUnitsDiscrepancy =
                riskSummary.TotalUnitsDiscrepancy
        };

        context.AuditRuns.Add(auditRun);

        foreach (AuditResult result in results)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (
                idempotencyKey is not null &&
                exception.InnerException is PostgresException postgres &&
                postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                postgres.ConstraintName == IdempotencyIndex)
        {
            var existing = await FindExistingAuditAsync(
                organizationId,
                storeId,
                warehouseId,
                idempotencyKey!,
                requestHash!,
                cancellationToken);

            if (existing is null)
            {
                throw;
            }

            return existing;
        }

        return new AuditSaveResult(
            auditRun.Id,
            completedAtUtc);
    }

    public async Task<PersistedAuditSnapshot?> TryGetExistingAuditAsync(
        Guid organizationId,
        Guid storeId,
        Guid warehouseId,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ValidateIdempotency(idempotencyKey, requestHash);

        var existing = await FindExistingAuditAsync(
            organizationId,
            storeId,
            warehouseId,
            idempotencyKey,
            requestHash,
            cancellationToken);

        if (existing is null)
        {
            return null;
        }

        return await GetPersistedAuditAsync(
            organizationId,
            existing.AuditId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "The existing audit could not be retrieved.");
    }


    private async Task<AuditSaveResult?> FindExistingAuditAsync(
        Guid organizationId,
        Guid storeId,
        Guid warehouseId,
        string idempotencyKey,
        string requestHash,
        CancellationToken cancellationToken)
    {
        await using var context =
            new StockGuardDbContext(_options);

        var existing = await context.AuditRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId &&
                     x.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existing is null)
        {
            return null;
        }

        if (existing.StoreId != storeId ||
            existing.WarehouseId != warehouseId ||
            !string.Equals(
                existing.RequestHash,
                requestHash,
                StringComparison.Ordinal))
        {
            throw new IdempotencyConflictException();
        }

        if (existing.CompletedAtUtc is not DateTime completedAtUtc ||
            existing.Status != "Completed")
        {
            throw new InvalidOperationException(
                "The existing audit is not completed.");
        }

        return new AuditSaveResult(
            existing.Id,
            completedAtUtc);
    }

    public async Task<PersistedAuditSnapshot?> GetPersistedAuditAsync(
        Guid organizationId,
        Guid auditId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var context =
            new StockGuardDbContext(_options);

        var audit = await context.AuditRuns
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.OrganizationId == organizationId &&
                     x.Id == auditId,
                cancellationToken);

        if (audit is null)
        {
            return null;
        }

        if (audit.Status != "Completed" ||
            audit.CompletedAtUtc is not DateTime completedAtUtc)
        {
            throw new InvalidOperationException(
                "The requested audit is not completed.");
        }

        var results = await context.AuditResults
            .AsNoTracking()
            .Where(x =>
                x.AuditRunId == auditId &&
                x.AuditRun.OrganizationId == organizationId)
            .Select(x => new AuditResult
            {
                SKU = x.SKU,
                ShopifyQuantity = x.ShopifyQuantity,
                WarehouseQuantity = x.WarehouseQuantity,
                Difference = x.Difference,
                Status = x.Status,
                Severity = x.Severity
            })
            .ToListAsync(cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (results.Count != audit.TotalProducts)
        {
            throw new InvalidOperationException(
                "The persisted audit results are incomplete.");
        }

        results = results
            .OrderBy(x => x.SKU, StringComparer.Ordinal)
            .ToList();

        return new PersistedAuditSnapshot(
            audit.Id,
            completedAtUtc,
            audit.TotalProducts,
            audit.MatchingProducts,
            audit.MismatchedProducts,
            audit.MissingProducts,
            results.Count(x => x.Severity == "High"),
            results.Count(x => x.Severity == "Medium"),
            results.Count(x => x.Severity == "Low"),
            audit.TotalUnitsDiscrepancy,
            results.AsReadOnly());
    }

    private static void ValidateIdempotency(
        string? idempotencyKey,
        string? requestHash)
    {
        if (idempotencyKey is null && requestHash is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(idempotencyKey) ||
            idempotencyKey.Length > 128 ||
            idempotencyKey.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Invalid idempotency key.",
                nameof(idempotencyKey));
        }

        if (requestHash is null ||
            requestHash.Length != 64 ||
            !requestHash.All(c =>
                (c >= '0' && c <= '9') ||
                (c >= 'A' && c <= 'F')))
        {
            throw new ArgumentException(
                "Invalid SHA-256 request hash.",
                nameof(requestHash));
        }
    }

    private static DateTime NormalizeTimestamp(
        DateTime timestamp)
    {
        if (timestamp.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Audit timestamps must be UTC.",
                nameof(timestamp));
        }

        return new DateTime(
            timestamp.Ticks - timestamp.Ticks % 10,
            DateTimeKind.Utc);
    }
}
