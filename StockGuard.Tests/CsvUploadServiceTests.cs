using System.Text;
using StockGuard.Infrastructure.Services;
using Xunit;

namespace StockGuard.Tests;

public class CsvUploadServiceTests
{
    private readonly CsvUploadService _service = new();

    private static MemoryStream CreateStream(string content)
    {
        return new MemoryStream(
            Encoding.UTF8.GetBytes(content));
    }

    [Fact]
    public void ReadWarehouseInventory_ValidCsv_ShouldSucceed()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSHIRT-001,100\nMUG-001,50");

        var results = _service.ReadWarehouseInventory(stream);

        Assert.Equal(2, results.Count);
        Assert.Equal("SHIRT-001", results[0].SKU);
        Assert.Equal(100, results[0].Quantity);
        Assert.Equal("MUG-001", results[1].SKU);
        Assert.Equal(50, results[1].Quantity);
    }

    [Fact]
    public void ReadShopifyInventory_ValidCsv_ShouldSucceed()
    {
        using var stream = CreateStream(
            "Handle,Title,SKU,Location,On hand (current)\n" +
            "shirt-blue,Blue Shirt,SHIRT-001,Main Warehouse,100");

        var results = _service.ReadShopifyInventory(stream);

        var item = Assert.Single(results);

        Assert.Equal("SHIRT-001", item.SKU);
        Assert.Equal(100, item.Quantity);
    }

    [Fact]
    public void ReadWarehouseInventory_DuplicateSku_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSHIRT-001,100\nshirt-001,50");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void ReadWarehouseInventory_NegativeQuantity_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSHIRT-001,-10");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void ReadWarehouseInventory_InvalidQuantity_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSHIRT-001,abc");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void ReadWarehouseInventory_MissingColumn_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Price\nSHIRT-001,100");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void ReadWarehouseInventory_EmptyFile_ShouldFail()
    {
        using var stream = CreateStream("");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void ReadWarehouseInventory_EmptySku_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\n,100");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void ReadWarehouseInventory_QuotedSku_ShouldSucceed()
    {
        using var stream = CreateStream(
            "SKU,Quantity\n\"SHIRT,BLUE\",100");

        var results = _service.ReadWarehouseInventory(stream);

        var item = Assert.Single(results);

        Assert.Equal("SHIRT,BLUE", item.SKU);
        Assert.Equal(100, item.Quantity);
    }

    [Fact]
    public void ReadWarehouseInventory_LowercaseHeaders_ShouldSucceed()
    {
        using var stream = CreateStream(
            "sku,quantity\nshirt-001,100");

        var results = _service.ReadWarehouseInventory(stream);

        var item = Assert.Single(results);

        Assert.Equal("SHIRT-001", item.SKU);
        Assert.Equal(100, item.Quantity);
    }

    [Fact]
    public void ReadWarehouseInventory_ZeroQuantity_ShouldSucceed()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSHIRT-001,0");

        var results = _service.ReadWarehouseInventory(stream);

        Assert.Equal(0, Assert.Single(results).Quantity);
    }

    [Fact]
    public void ReadWarehouseInventory_HeaderOnly_ShouldFail()
    {
        using var stream = CreateStream("SKU,Quantity");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void ReadWarehouseInventory_DuplicateHeaders_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,SKU,Quantity\nSHIRT-001,SHIRT-002,100");

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void ReadWarehouseInventory_TooManyRecords_ShouldFail()
    {
        var content = new StringBuilder("SKU,Quantity\n");

        for (int i = 0; i <= CsvUploadService.MaxRecords; i++)
        {
            content.AppendLine($"SKU-{i},100");
        }

        using var stream = CreateStream(content.ToString());

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void ReadWarehouseInventory_OversizedFile_ShouldFail()
    {
        using var stream = new MemoryStream(
            new byte[CsvUploadService.MaxFileSize + 1]);

        Assert.Throws<InvalidDataException>(() =>
            _service.ReadWarehouseInventory(stream));
    }
}
