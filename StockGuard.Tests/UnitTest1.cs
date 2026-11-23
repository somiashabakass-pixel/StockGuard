
using StockGuard.ConsoleApp.Models;
using StockGuard.ConsoleApp.Services;
using Xunit;

namespace StockGuard.Tests;

public class InventoryAuditTests
{
    [Fact]
    public void CompareInventory_DetectsQuantityMismatch()
    {
        List<InventoryRecord> shopify = new()
        {
            new InventoryRecord { SKU = "SKU-001", Quantity = 100 }
        };

        List<InventoryRecord> warehouse = new()
        {
            new InventoryRecord { SKU = "SKU-001", Quantity = 92 }
        };

        InventoryAuditService service = new();

        List<AuditResult> results =
            service.CompareInventory(shopify, warehouse);

        Assert.Single(results);
        Assert.Equal("Mismatch", results[0].Status);
        Assert.Equal(8, results[0].Difference);
    }

    [Fact]
    public void CompareInventory_DetectsMatchingQuantities()
    {
        List<InventoryRecord> shopify = new()
        {
            new InventoryRecord { SKU = "SKU-001", Quantity = 50 }
        };

        List<InventoryRecord> warehouse = new()
        {
            new InventoryRecord { SKU = "SKU-001", Quantity = 50 }
        };

        InventoryAuditService service = new();

        List<AuditResult> results =
            service.CompareInventory(shopify, warehouse);

        Assert.Single(results);
        Assert.Equal("Match", results[0].Status);
        Assert.Equal(0, results[0].Difference);
    }

    [Fact]
    public void CompareInventory_DetectsMissingWarehouseProduct()
    {
        List<InventoryRecord> shopify = new()
        {
            new InventoryRecord { SKU = "SKU-005", Quantity = 20 }
        };

        List<InventoryRecord> warehouse = new();

        InventoryAuditService service = new();

        List<AuditResult> results =
            service.CompareInventory(shopify, warehouse);

        Assert.Single(results);
        Assert.Equal("Missing from Warehouse", results[0].Status);
        Assert.Null(results[0].WarehouseQuantity);
        Assert.Null(results[0].Difference);
    }

    [Fact]
    public void CompareInventory_DetectsNegativeDifference()
    {
        List<InventoryRecord> shopify = new()
        {
            new InventoryRecord { SKU = "SKU-004", Quantity = 30 }
        };

        List<InventoryRecord> warehouse = new()
        {
            new InventoryRecord { SKU = "SKU-004", Quantity = 35 }
        };

        InventoryAuditService service = new();

        List<AuditResult> results =
            service.CompareInventory(shopify, warehouse);

        Assert.Single(results);
        Assert.Equal("Mismatch", results[0].Status);
        Assert.Equal(-5, results[0].Difference);
    }

[Fact]
public void CompareInventory_DetectsMissingShopifyProduct()
{
    List<InventoryRecord> shopify = new();

    List<InventoryRecord> warehouse = new()
    {
        new InventoryRecord { SKU = "SKU-006", Quantity = 40 }
    };

    InventoryAuditService service = new();

    List<AuditResult> results =
        service.CompareInventory(shopify, warehouse);

    Assert.Single(results);
    Assert.Equal("Missing from Shopify", results[0].Status);
    Assert.Null(results[0].ShopifyQuantity);
    Assert.Equal(40, results[0].WarehouseQuantity);
    Assert.Null(results[0].Difference);
}

[Fact]
public void CompareInventory_HandlesMultipleProducts()
{
    List<InventoryRecord> shopify = new()
    {
        new InventoryRecord { SKU = "SKU-001", Quantity = 100 },
        new InventoryRecord { SKU = "SKU-002", Quantity = 50 },
        new InventoryRecord { SKU = "SKU-003", Quantity = 75 }
    };

    List<InventoryRecord> warehouse = new()
    {
        new InventoryRecord { SKU = "SKU-001", Quantity = 92 },
        new InventoryRecord { SKU = "SKU-002", Quantity = 50 },
        new InventoryRecord { SKU = "SKU-004", Quantity = 30 }
    };

    InventoryAuditService service = new();

    List<AuditResult> results =
        service.CompareInventory(shopify, warehouse);

    Assert.Equal(4, results.Count);

    Assert.Equal("Mismatch",
        results.Single(r => r.SKU == "SKU-001").Status);

    Assert.Equal("Match",
        results.Single(r => r.SKU == "SKU-002").Status);

    Assert.Equal("Missing from Warehouse",
        results.Single(r => r.SKU == "SKU-003").Status);

    Assert.Equal("Missing from Shopify",
        results.Single(r => r.SKU == "SKU-004").Status);
}

}
