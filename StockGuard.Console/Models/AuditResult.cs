
namespace StockGuard.ConsoleApp.Models;

public class AuditResult
{
    public string SKU { get; set; } = string.Empty;

    public int? ShopifyQuantity { get; set; }

    public int? WarehouseQuantity { get; set; }

    public string Status { get; set; } = string.Empty;

    public int? Difference { get; set; }

    // NEW: Inventory discrepancy severity
    public string Severity { get; set; } = "None";
}
