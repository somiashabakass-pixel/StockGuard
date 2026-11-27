using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class JsonAuditEndpointTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public JsonAuditEndpointTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
        _factory.VerifyDatabaseIsolation();
    }

    [Fact]
    public async Task Compare_ValidInventory_ReturnsCorrectSummary()
    {
        using var client = _factory.CreateClient();

        var request = new
        {
            shopifyInventory = new[]
            {
                new { sku = "SHIRT-001", quantity = 100 },
                new { sku = "SHIRT-002", quantity = 50 },
                new { sku = "MUG-001", quantity = 75 }
            },
            warehouseInventory = new[]
            {
                new { sku = "SHIRT-001", quantity = 92 },
                new { sku = "SHIRT-002", quantity = 50 },
                new { sku = "MUG-001", quantity = 60 },
                new { sku = "SKU-006", quantity = 40 }
            }
        };

        using var response = await client.PostAsJsonAsync(
            "/api/audits/compare", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;

        Assert.Equal(4,
            root.GetProperty("totalProducts").GetInt32());

        Assert.Equal(1,
            root.GetProperty("matchingProducts").GetInt32());

        Assert.Equal(2,
            root.GetProperty("mismatchedProducts").GetInt32());

        Assert.Equal(1,
            root.GetProperty("missingProducts").GetInt32());

        Assert.Equal(2,
            root.GetProperty("mediumSeverity").GetInt32());

        Assert.Equal(23,
            root.GetProperty("totalUnitsDiscrepancy").GetInt32());

        Assert.Equal(JsonValueKind.Null,
            root.GetProperty("auditId").ValueKind);

        Assert.Equal(4,
            root.GetProperty("results").GetArrayLength());
    }

    [Theory]
    [InlineData(100, 100, "Match", "None")]
    [InlineData(100, 97, "Mismatch", "Low")]
    [InlineData(100, 90, "Mismatch", "Medium")]
    [InlineData(100, 50, "Mismatch", "High")]
    public async Task Compare_QuantityDifference_ReturnsCorrectSeverity(
        int shopifyQuantity,
        int warehouseQuantity,
        string expectedStatus,
        string expectedSeverity)
    {
        using var client = _factory.CreateClient();

        var request = new
        {
            shopifyInventory = new[]
            {
                new
                {
                    sku = "TEST-001",
                    quantity = shopifyQuantity
                }
            },
            warehouseInventory = new[]
            {
                new
                {
                    sku = "TEST-001",
                    quantity = warehouseQuantity
                }
            }
        };

        using var response = await client.PostAsJsonAsync(
            "/api/audits/compare", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var result = document.RootElement
            .GetProperty("results")[0];

        Assert.Equal(expectedStatus,
            result.GetProperty("status").GetString());

        Assert.Equal(expectedSeverity,
            result.GetProperty("severity").GetString());

        Assert.Equal(shopifyQuantity - warehouseQuantity,
            result.GetProperty("difference").GetInt32());
    }

    [Fact]
    public async Task Compare_NegativeQuantity_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        var request = new
        {
            shopifyInventory = new[]
            {
                new { sku = "TEST-001", quantity = -10 }
            },
            warehouseInventory = new[]
            {
                new { sku = "TEST-001", quantity = 10 }
            }
        };

        using var response = await client.PostAsJsonAsync(
            "/api/audits/compare", request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Compare_DuplicateSku_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        var request = new
        {
            shopifyInventory = new[]
            {
                new { sku = "TEST-001", quantity = 10 },
                new { sku = "test-001", quantity = 20 }
            },
            warehouseInventory = new[]
            {
                new { sku = "TEST-001", quantity = 10 }
            }
        };

        using var response = await client.PostAsJsonAsync(
            "/api/audits/compare", request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Compare_EmptyInventory_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        var request = new
        {
            shopifyInventory = Array.Empty<object>(),
            warehouseInventory = new[]
            {
                new { sku = "TEST-001", quantity = 10 }
            }
        };

        using var response = await client.PostAsJsonAsync(
            "/api/audits/compare", request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Compare_EmptySku_ReturnsBadRequest()
    {
        using var client = _factory.CreateClient();

        var request = new
        {
            shopifyInventory = new[]
            {
                new { sku = "", quantity = 10 }
            },
            warehouseInventory = new[]
            {
                new { sku = "TEST-001", quantity = 10 }
            }
        };

        using var response = await client.PostAsJsonAsync(
            "/api/audits/compare", request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task Compare_NormalizedSku_MatchesCorrectly()
    {
        using var client = _factory.CreateClient();

        var request = new
        {
            shopifyInventory = new[]
            {
                new { sku = " test-001 ", quantity = 50 }
            },
            warehouseInventory = new[]
            {
                new { sku = "TEST-001", quantity = 50 }
            }
        };

        using var response = await client.PostAsJsonAsync(
            "/api/audits/compare", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        var root = document.RootElement;

        Assert.Equal(1,
            root.GetProperty("totalProducts").GetInt32());

        Assert.Equal(1,
            root.GetProperty("matchingProducts").GetInt32());

        Assert.Equal(0,
            root.GetProperty("totalUnitsDiscrepancy").GetInt32());

        Assert.Equal("TEST-001",
            root.GetProperty("results")[0]
                .GetProperty("sku").GetString());
    }
}
