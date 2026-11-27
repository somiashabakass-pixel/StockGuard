using System.Text;
using StockGuard.Infrastructure.Services;
using Xunit;

namespace StockGuard.Tests;

public class CsvMalformedTests
{
    private readonly CsvUploadService _service = new();

    private MemoryStream CreateStream(string content)
    {
        return new MemoryStream(
            Encoding.UTF8.GetBytes(content));
    }

    [Fact]
    public void Warehouse_UnclosedQuote_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\n\"SKU-001,100\n");

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_UnexpectedQuote_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSKU\"001,100\n");

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_CharactersAfterQuote_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\n\"SKU-001\"ABC,100\n");

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_MissingField_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSKU-001\n");

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_ExtraField_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity\nSKU-001,100,EXTRA\n");

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_EscapedQuote_ShouldSucceed()
    {
        using var stream = CreateStream(
            "SKU,Quantity\n\"SKU-\"\"001\"\"\",100\n");

        var result = _service.ReadWarehouseInventory(stream);
        var item = Assert.Single(result);

        Assert.Equal("SKU-\"001\"", item.SKU);
        Assert.Equal(100, item.Quantity);
    }

    [Fact]
    public void Warehouse_MultilineDescription_ShouldSucceed()
    {
        using var stream = CreateStream(
            "SKU,Quantity,Description\n" +
            "SKU-001,100,\"First line\nSecond line\"\n");

        var result = _service.ReadWarehouseInventory(stream);
        var item = Assert.Single(result);

        Assert.Equal("SKU-001", item.SKU);
        Assert.Equal(100, item.Quantity);
    }

    [Fact]
    public void Warehouse_EmptyHeader_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity,\nSKU-001,100,Description\n");

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }
}
