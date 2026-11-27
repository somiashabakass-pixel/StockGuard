using System.Text;
using StockGuard.Infrastructure.Services;
using Xunit;

namespace StockGuard.Tests;

public class CsvHeaderValidationTests
{
    private readonly CsvUploadService _service = new();

    private MemoryStream CreateStream(string content)
    {
        return new MemoryStream(
            Encoding.UTF8.GetBytes(content));
    }

    [Fact]
    public void Warehouse_WhitespaceHeader_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity,   \nSKU-001,100,Example\n");

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_DuplicateNormalizedHeader_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity, sku \nSKU-001,100,SKU-002\n");

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_ControlCharacterInHeader_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Quantity,Des\u0000cription\n" +
            "SKU-001,100,Example\n");

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_OversizedHeader_ShouldFail()
    {
        string largeHeader = new string('A', 65537);

        using var stream = CreateStream(
            $"SKU,Quantity,{largeHeader}\n" +
            "SKU-001,100,Example\n");

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_ReorderedHeaders_ShouldSucceed()
    {
        using var stream = CreateStream(
            "Description,Quantity,SKU\n" +
            "Example,100,SKU-001\n");

        var result = _service.ReadWarehouseInventory(stream);
        var item = Assert.Single(result);

        Assert.Equal("SKU-001", item.SKU);
        Assert.Equal(100, item.Quantity);
    }

    [Fact]
    public void Warehouse_TrimmedHeaders_ShouldSucceed()
    {
        using var stream = CreateStream(
            " SKU , Quantity \nSKU-001,100\n");

        var result = _service.ReadWarehouseInventory(stream);
        var item = Assert.Single(result);

        Assert.Equal("SKU-001", item.SKU);
        Assert.Equal(100, item.Quantity);
    }

    [Fact]
    public void Shopify_DuplicateLocationHeader_ShouldFail()
    {
        using var stream = CreateStream(
            "SKU,Location,On hand (current),location\n" +
            "SKU-001,Main Warehouse,100,Main Warehouse\n");

        Assert.Throws<InvalidDataException>(
            () => _service.ReadShopifyInventory(stream));
    }

    [Fact]
    public void Shopify_AdditionalValidHeader_ShouldSucceed()
    {
        using var stream = CreateStream(
            "Title,SKU,Location,On hand (current)\n" +
            "Blue Shirt,SKU-001,Main Warehouse,100\n");

        var result = _service.ReadShopifyInventory(stream);
        var item = Assert.Single(result);

        Assert.Equal("SKU-001", item.SKU);
        Assert.Equal(100, item.Quantity);
    }
}
