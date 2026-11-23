using StockGuard.Core.Services;

using StockGuard.Core.Models;
using StockGuard.Infrastructure.Services;
using Xunit;

namespace StockGuard.Tests;

public class InventorySeverityTests
{
    [Theory]
    [InlineData(100, 100, "None")]
    [InlineData(100, 99, "Low")]
    [InlineData(100, 95, "Low")]
    [InlineData(100, 94, "Medium")]
    [InlineData(100, 80, "Medium")]
    [InlineData(100, 79, "High")]
    [InlineData(75, 100, "High")]
    public void CompareInventory_AssignsCorrectSeverity(
        int shopifyQuantity,
        int warehouseQuantity,
        string expectedSeverity)
    {
        InventoryAuditService service = new();

        List<InventoryRecord> shopify = new()
        {
            new InventoryRecord
            {
                SKU = "TEST-001",
                Quantity = shopifyQuantity
            }
        };

        List<InventoryRecord> warehouse = new()
        {
            new InventoryRecord
            {
                SKU = "TEST-001",
                Quantity = warehouseQuantity
            }
        };

        List<AuditResult> results =
            service.CompareInventory(shopify, warehouse);

        Assert.Single(results);
        Assert.Equal(expectedSeverity, results[0].Severity);
    }

    [Fact]
    public void CompareInventory_MissingProductHasMissingSeverity()
    {
        InventoryAuditService service = new();

        List<InventoryRecord> shopify = new()
        {
            new InventoryRecord
            {
                SKU = "MISSING-001",
                Quantity = 10
            }
        };

        List<InventoryRecord> warehouse = new();

        List<AuditResult> results =
            service.CompareInventory(shopify, warehouse);

        Assert.Single(results);
        Assert.Equal("Missing", results[0].Severity);
        Assert.Equal("Missing from Warehouse", results[0].Status);
    }
}



