using System.Text;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Services;
using Xunit;

namespace StockGuard.Tests;

public class CancellationTests
{
    [Fact]
    public void ShopifyCsv_CancelledToken_Throws()
    {
        var service = new CsvUploadService();

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                "SKU,Location,On hand (current)\nSKU-001,Main,100"));

        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            service.ReadShopifyInventory(
                stream, source.Token));
    }

    [Fact]
    public void WarehouseCsv_CancelledToken_Throws()
    {
        var service = new CsvUploadService();

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(
                "SKU,Quantity\nSKU-001,100"));

        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            service.ReadWarehouseInventory(
                stream, source.Token));
    }

    [Fact]
    public void CompareInventory_CancelledToken_Throws()
    {
        var service = new InventoryAuditService();

        var shopify = new List<InventoryRecord>
        {
            new() { SKU = "SKU-001", Quantity = 100 }
        };

        var warehouse = new List<InventoryRecord>
        {
            new() { SKU = "SKU-001", Quantity = 90 }
        };

        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            service.CompareInventory(
                shopify, warehouse, source.Token));
    }

    [Fact]
    public void CalculateRisk_CancelledToken_Throws()
    {
        var service = new InventoryRiskService();

        var results = new List<AuditResult>
        {
            new()
            {
                SKU = "SKU-001",
                Difference = 10,
                Status = "Mismatch",
                Severity = "Medium"
            }
        };

        using var source = new CancellationTokenSource();
        source.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            service.CalculateRisk(
                results, source.Token));
    }

    [Fact]
    public void CompareInventory_ActiveToken_Completes()
    {
        var service = new InventoryAuditService();

        var shopify = new List<InventoryRecord>
        {
            new() { SKU = "SKU-001", Quantity = 100 }
        };

        var warehouse = new List<InventoryRecord>
        {
            new() { SKU = "SKU-001", Quantity = 90 }
        };

        using var source = new CancellationTokenSource();

        var results = service.CompareInventory(
            shopify, warehouse, source.Token);

        var result = Assert.Single(results);

        Assert.Equal("Mismatch", result.Status);
        Assert.Equal("Medium", result.Severity);
        Assert.Equal(10, result.Difference);
    }

    [Fact]
    public void CalculateRisk_ActiveToken_Completes()
    {
        var service = new InventoryRiskService();

        var results = new List<AuditResult>
        {
            new()
            {
                SKU = "SKU-001",
                Difference = 10,
                Status = "Mismatch",
                Severity = "Medium"
            }
        };

        using var source = new CancellationTokenSource();

        var summary = service.CalculateRisk(
            results, source.Token);

        Assert.Equal(1, summary.MediumSeverity);
        Assert.Equal(10, summary.TotalUnitsDiscrepancy);
    }
}
