using StockGuard.Core.Models;

namespace StockGuard.Infrastructure.Data;

public sealed record PersistedAuditSnapshot(
    Guid AuditId,
    DateTime CompletedAtUtc,
    int TotalProducts,
    int MatchingProducts,
    int MismatchedProducts,
    int MissingProducts,
    int HighSeverity,
    int MediumSeverity,
    int LowSeverity,
    int TotalUnitsDiscrepancy,
    IReadOnlyList<AuditResult> Results);
