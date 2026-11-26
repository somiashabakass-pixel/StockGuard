namespace StockGuard.Api.Dtos;

public class AuditResultDto
{
    public string SKU { get; set; } = string.Empty;

    public int? ShopifyQuantity { get; set; }

    public int? WarehouseQuantity { get; set; }

    public int? Difference { get; set; }

    public string Status { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;
}
