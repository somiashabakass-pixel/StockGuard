namespace StockGuard.Infrastructure.Data;

public sealed record AuditSaveResult(
    Guid AuditId,
    DateTime CompletedAtUtc);
