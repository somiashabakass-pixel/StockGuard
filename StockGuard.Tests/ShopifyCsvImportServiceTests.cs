
using StockGuard.Core.Models;
using StockGuard.Infrastructure.Services;
using Xunit;

namespace StockGuard.Tests;

public class ShopifyCsvImportServiceTests
{
    [Fact]
    public void LoadInventory_ReadsShopifyCsvCorrectly()
    {
        // ARRANGE: Create a temporary Shopify CSV file

        string fileName = $"shopify-test-{Guid.NewGuid()}.csv";

        string filePath = Path.Combine(
            AppContext.BaseDirectory,
            fileName);

        try
        {
            File.WriteAllLines(filePath, new[]
            {
                "Handle,Title,SKU,Location,On hand (current)",
                "shirt-blue,Blue Shirt,SHIRT-001,Main Warehouse,100",
                "shirt-red,Red Shirt,SHIRT-002,Main Warehouse,50",
                "mug-black,Black Mug,MUG-001,Main Warehouse,75"
            });

            // ACT: Import inventory

            ShopifyCsvImportService service = new();

            List<InventoryRecord> results =
                service.LoadInventory(fileName);

            // ASSERT: Verify imported records

            Assert.Equal(3, results.Count);

            Assert.Equal(100,
                results.Single(r => r.SKU == "SHIRT-001").Quantity);

            Assert.Equal(50,
                results.Single(r => r.SKU == "SHIRT-002").Quantity);

            Assert.Equal(75,
                results.Single(r => r.SKU == "MUG-001").Quantity);
        }
        finally
        {
            // Clean up the temporary test file

            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

[Fact]
public void LoadInventory_HandlesQuotedCommas()
{
    string fileName = $"shopify-test-{Guid.NewGuid()}.csv";
    string filePath = Path.Combine(
        AppContext.BaseDirectory, fileName);

    try
    {
        File.WriteAllLines(filePath, new[]
        {
            "Handle,Title,SKU,Location,On hand (current)",
            "shirt-blue,\"Blue Shirt, Large\",SHIRT-001,Main Warehouse,100"
        });

        ShopifyCsvImportService service = new();

        List<InventoryRecord> results =
            service.LoadInventory(fileName);

        Assert.Single(results);
        Assert.Equal("SHIRT-001", results[0].SKU);
        Assert.Equal(100, results[0].Quantity);
    }
    finally
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}

[Fact]
public void LoadInventory_RejectsInvalidQuantity()
{
    string fileName = $"shopify-test-{Guid.NewGuid()}.csv";
    string filePath = Path.Combine(
        AppContext.BaseDirectory, fileName);

    try
    {
        File.WriteAllLines(filePath, new[]
        {
            "Handle,Title,SKU,Location,On hand (current)",
            "shirt-blue,Blue Shirt,SHIRT-001,Main Warehouse,abc"
        });

        ShopifyCsvImportService service = new();

        Assert.Throws<FormatException>(() =>
            service.LoadInventory(fileName));
    }
    finally
    {
        if (File.Exists(filePath))
            File.Delete(filePath);
    }
}

}


