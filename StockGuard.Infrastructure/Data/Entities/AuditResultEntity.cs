namespace StockGuard.Infrastructure.Data.Entities;

public class AuditResultEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AuditRunId { get; set; }

    public string SKU { get; set; } = string.Empty;

    public int? ShopifyQuantity { get; set; }

    public int? WarehouseQuantity { get; set; }

    public int? Difference { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Severity { get; set; } = "None";

    public AuditRun AuditRun { get; set; } = null!;
}
