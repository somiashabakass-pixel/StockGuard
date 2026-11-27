using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class CsvUploadEndpointTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    private const string ValidShopify =
        "SKU,Location,On hand (current)\n" +
        "TEST-001,Main Warehouse,100\n";

    private const string ValidWarehouse =
        "SKU,Quantity\nTEST-001,90\n";

    public CsvUploadEndpointTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
        _factory.VerifyDatabaseIsolation();
    }

    private static void AddFile(
        MultipartFormDataContent form,
        string field,
        string filename,
        string content)
    {
        var file = new ByteArrayContent(
            Encoding.UTF8.GetBytes(content));

        file.Headers.ContentType =
            new MediaTypeHeaderValue("text/csv");

        form.Add(file, field, filename);
    }

    [Theory]
    [InlineData("ShopifyFile")]
    [InlineData("WarehouseFile")]
    public async Task Upload_MissingFile_ReturnsBadRequest(
        string missingField)
    {
        using var client = _factory.CreateClient();
        using var form = new MultipartFormDataContent();

        if (missingField != "ShopifyFile")
        {
            AddFile(form, "ShopifyFile",
                "shopify.csv", ValidShopify);
        }

        if (missingField != "WarehouseFile")
        {
            AddFile(form, "WarehouseFile",
                "warehouse.csv", ValidWarehouse);
        }

        using var response = await client.PostAsync(
            "/api/audits/upload", form);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Theory]
    [InlineData("warehouse.txt", "SKU,Quantity\nTEST-001,90\n")]
    [InlineData("warehouse.csv", "SKU,Quantity\nTEST-001,-10\n")]
    [InlineData("warehouse.csv", "SKU,Quantity\nTEST-001,abc\n")]
    [InlineData("warehouse.csv", "SKU,Quantity\nTEST-001,10\nTEST-001,20\n")]
    [InlineData("warehouse.csv", "Product,Amount\nTEST-001,10\n")]
    [InlineData("warehouse.csv", "")]
    [InlineData("warehouse.csv", "SKU,Quantity\n")]
    public async Task Upload_InvalidWarehouse_ReturnsBadRequest(
        string filename,
        string content)
    {
        using var client = _factory.CreateClient();
        using var form = new MultipartFormDataContent();

        AddFile(form, "ShopifyFile",
            "shopify.csv", ValidShopify);

        AddFile(form, "WarehouseFile",
            filename, content);

        using var response = await client.PostAsync(
            "/api/audits/upload", form);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);

        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(400,
            document.RootElement
                .GetProperty("status").GetInt32());

        Assert.True(
            document.RootElement
                .TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Upload_InvalidShopify_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();
        using var form = new MultipartFormDataContent();

        AddFile(form, "ShopifyFile",
            "shopify.csv",
            "SKU,Location,On hand (current)\n" +
            "TEST-001,Main Warehouse,-50\n");

        AddFile(form, "WarehouseFile",
            "warehouse.csv", ValidWarehouse);

        using var response = await client.PostAsync(
            "/api/audits/upload", form);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }
}
