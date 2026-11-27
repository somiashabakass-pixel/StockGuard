namespace StockGuard.Infrastructure.Data.Entities;

public class AuditRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public Guid StoreId { get; set; }

    public Guid WarehouseId { get; set; }

    public string? IdempotencyKey { get; set; }

    public string? RequestHash { get; set; }

    public DateTime StartedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? CompletedAtUtc { get; set; }

    public string Status { get; set; } = "Pending";

    public int TotalProducts { get; set; }

    public int MatchingProducts { get; set; }

    public int MismatchedProducts { get; set; }

    public int MissingProducts { get; set; }

    public int TotalUnitsDiscrepancy { get; set; }

    public Organization Organization { get; set; }
        = null!;

    public Store Store { get; set; } = null!;

    public Warehouse Warehouse { get; set; } = null!;
}
