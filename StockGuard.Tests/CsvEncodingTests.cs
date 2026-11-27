using System.Text;
using StockGuard.Infrastructure.Services;
using Xunit;

namespace StockGuard.Tests;

public class CsvEncodingTests
{
    private readonly CsvUploadService _service = new();

    [Fact]
    public void Warehouse_Utf8WithoutBom_ShouldSucceed()
    {
        string content =
            "SKU,Quantity\nSKU-001,100\n";

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(content));

        var result = _service.ReadWarehouseInventory(stream);

        var item = Assert.Single(result);

        Assert.Equal("SKU-001", item.SKU);
        Assert.Equal(100, item.Quantity);
    }

    [Fact]
    public void Warehouse_Utf8WithBom_ShouldSucceed()
    {
        string content =
            "SKU,Quantity\nSKU-001,100\n";

        byte[] bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(content))
            .ToArray();

        using var stream = new MemoryStream(bytes);

        var result = _service.ReadWarehouseInventory(stream);

        var item = Assert.Single(result);

        Assert.Equal("SKU-001", item.SKU);
        Assert.Equal(100, item.Quantity);
    }

    [Fact]
    public void Shopify_Utf8WithBom_ShouldSucceed()
    {
        string content =
            "SKU,Location,On hand (current)\n" +
            "SKU-001,Main Warehouse,75\n";

        byte[] bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(content))
            .ToArray();

        using var stream = new MemoryStream(bytes);

        var result = _service.ReadShopifyInventory(stream);

        var item = Assert.Single(result);

        Assert.Equal("SKU-001", item.SKU);
        Assert.Equal(75, item.Quantity);
    }

    [Fact]
    public void Warehouse_UnicodeSku_ShouldSucceed()
    {
        string content =
            "SKU,Quantity\nCAF\u00C9-001,25\n";

        using var stream = new MemoryStream(
            Encoding.UTF8.GetBytes(content));

        var result = _service.ReadWarehouseInventory(stream);

        var item = Assert.Single(result);

        Assert.Equal("CAF\u00C9-001", item.SKU);
        Assert.Equal(25, item.Quantity);
    }

    [Fact]
    public void Warehouse_InvalidUtf8_ShouldFail()
    {
        byte[] header = Encoding.UTF8.GetBytes(
            "SKU,Quantity\n");

        byte[] invalidBytes = header
            .Concat(new byte[] { 0xFF, 0xFE })
            .Concat(Encoding.UTF8.GetBytes(",10\n"))
            .ToArray();

        using var stream = new MemoryStream(invalidBytes);

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }

    [Fact]
    public void Warehouse_Utf16_ShouldFail()
    {
        string content =
            "SKU,Quantity\nSKU-001,100\n";

        byte[] bytes = Encoding.Unicode.GetPreamble()
            .Concat(Encoding.Unicode.GetBytes(content))
            .ToArray();

        using var stream = new MemoryStream(bytes);

        Assert.Throws<InvalidDataException>(
            () => _service.ReadWarehouseInventory(stream));
    }
}
