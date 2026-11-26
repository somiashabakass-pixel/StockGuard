using System.Text;
using StockGuard.Infrastructure.Services;
using Xunit;

namespace StockGuard.Tests;

public class CsvUploadAdvancedTests
{
    private readonly CsvUploadService _service = new();

    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(
            Encoding.UTF8.GetBytes(content));
    }

    [Fact]
    public void Shopify_MultipleLocations_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Location,On hand (current)\n" +
            "SKU-001,Warehouse A,100\n" +
            "SKU-002,Warehouse B,50");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadShopifyInventory(stream));
    }

    [Fact]
    public void Shopify_SingleLocation_ShouldSucceed()
    {
        using var stream = CreateStream(
            "SKU,Location,On hand (current)\n" +
            "SKU-001,Warehouse A,100\n" +
            "SKU-002,Warehouse A,50");

        var results = _service.ReadShopifyInventory(stream);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Shopify_MissingLocationColumn_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,On hand (current)\nSKU-001,100");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadShopifyInventory(stream));
    }

    [Fact]
    public void Shopify_EmptyLocation_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Location,On hand (current)\n" +
            "SKU-001,,100");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadShopifyInventory(stream));
    }

    [Fact]
    public void Shopify_LocationCaseDifference_ShouldSucceed()
    {
        using var stream = CreateStream(
            "SKU,Location,On hand (current)\n" +
            "SKU-001,Warehouse A,100\n" +
            "SKU-002,warehouse a,50");

        var results = _service.ReadShopifyInventory(stream);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Warehouse_OversizedSku_ShouldFail()
    {
        string sku = new string('A', 256);

        using var stream = CreateStream(
            $"SKU,Quantity\n{sku},100");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_ControlCharacterInSku_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\n\"SKU\t001\",100");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_ExtraColumn_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSKU-001,100,Extra");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_MissingQuantity_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSKU-001");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_QuantityOverflow_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSKU-001,2147483648");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_MaximumIntegerQuantity_ShouldSucceed()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSKU-001,2147483647");

        var result = Assert.Single(
            _service.ReadWarehouseInventory(stream));

        Assert.Equal(int.MaxValue, result.Quantity);
    }

    [Fact]
    public void Warehouse_InvalidUtf8_ShouldFail()
    {
        byte[] header = Encoding.UTF8.GetBytes(
            "SKU,Quantity\n");

        byte[] invalidBytes = header
            .Concat(new byte[] { 0xFF, 0xFE })
            .Concat(Encoding.UTF8.GetBytes(",100"))
            .ToArray();

        using var stream = new MemoryStream(invalidBytes);

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_OversizedField_ShouldFail()
    {
        string field = new string('A', 65537);

        using var stream = CreateStream(
            $"SKU,Quantity,Description\nSKU-001,100,{field}");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_MaximumRecords_ShouldSucceed()
    {
        var content = new StringBuilder("SKU,Quantity\n");

        for (int i = 0; i < CsvUploadService.MaxRecords; i++)
        {
            content.AppendLine($"SKU-{i},100");
        }

        using var stream = CreateStream(content.ToString());

        var results = _service.ReadWarehouseInventory(stream);

        Assert.Equal(CsvUploadService.MaxRecords, results.Count);
    }

    [Fact]
    public void Warehouse_MultilineDescription_ShouldSucceed()
    {
        using var stream = CreateStream(
            "SKU,Quantity,Description\n" +
            "SKU-001,100,\"First line\nSecond line\"");

        var result = Assert.Single(
            _service.ReadWarehouseInventory(stream));

        Assert.Equal("SKU-001", result.SKU);
        Assert.Equal(100, result.Quantity);
    }
}
