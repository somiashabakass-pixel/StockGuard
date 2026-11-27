using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data;
using StockGuard.Tests.Integration;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class CrossFormatIdempotencyTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly HttpClient _client;

    public CrossFormatIdempotencyTests(
        StockGuardApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task JsonAndCsv_ShouldShareIdempotency(
        bool jsonFirst,
        bool conflictingRequest)
    {
        await HttpTestData.InitializeAsync();

        string sku = $"CROSS-{Guid.NewGuid():N}"
            .ToUpperInvariant();

        string key = Guid.NewGuid().ToString("N");

        Guid organizationId =
            DevelopmentDataService.OrganizationId;

        async Task<HttpResponseMessage> SendAsync(
            bool useJson,
            int warehouseQuantity)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                useJson
                    ? "/api/audits/save"
                    : "/api/audits/upload");

            request.Headers.Add(
                "Idempotency-Key", key);

            if (useJson)
            {
                request.Content = JsonContent.Create(new
                {
                    shopifyInventory = new[]
                    {
                        new
                        {
                            sku = sku.ToLowerInvariant(),
                            quantity = 100
                        }
                    },
                    warehouseInventory = new[]
                    {
                        new
                        {
                            sku,
                            quantity = warehouseQuantity
                        }
                    }
                });
            }
            else
            {
                var form = new MultipartFormDataContent();

                string shopifyCsv =
                    "SKU,Location,On hand (current)\n" +
                    $"{sku},Main Warehouse,100\n";

                string warehouseCsv =
                    "SKU,Quantity\n" +
                    $"{sku},{warehouseQuantity}\n";

                form.Add(
                    new StringContent(
                        shopifyCsv,
                        Encoding.UTF8,
                        "text/csv"),
                    "ShopifyFile",
                    "shopify.csv");

                form.Add(
                    new StringContent(
                        warehouseCsv,
                        Encoding.UTF8,
                        "text/csv"),
                    "WarehouseFile",
                    "warehouse.csv");

                request.Content = form;
            }

            return await _client.SendAsync(request);
        }

        try
        {
            using var first = await SendAsync(
                jsonFirst, 90);

            Assert.Equal(
                HttpStatusCode.OK,
                first.StatusCode);

            string firstBody =
                await first.Content.ReadAsStringAsync();

            var firstJson = JsonNode.Parse(firstBody)!;

            Guid auditId =
                firstJson["auditId"]!.GetValue<Guid>();

            Assert.NotEqual(Guid.Empty, auditId);

            using var second = await SendAsync(
                !jsonFirst,
                conflictingRequest ? 80 : 90);

            if (conflictingRequest)
            {
                Assert.Equal(
                    HttpStatusCode.Conflict,
                    second.StatusCode);
            }
            else
            {
                Assert.Equal(
                    HttpStatusCode.OK,
                    second.StatusCode);

                string secondBody =
                    await second.Content.ReadAsStringAsync();

                var secondJson =
                    JsonNode.Parse(secondBody);

                Assert.True(
                    JsonNode.DeepEquals(
                        firstJson, secondJson),
                    "JSON and CSV audit responses differ.");
            }

            await using var verification =
                TestDatabase.CreateContext();

            var savedAudits = await verification.AuditRuns
                .AsNoTracking()
                .Where(x =>
                    x.OrganizationId == organizationId &&
                    x.IdempotencyKey == key)
                .ToListAsync();

            var savedAudit = Assert.Single(savedAudits);

            Assert.Equal(auditId, savedAudit.Id);

            Assert.Equal(
                10,
                savedAudit.TotalUnitsDiscrepancy);

            var savedResults = await verification.AuditResults
                .AsNoTracking()
                .Where(x => x.AuditRunId == auditId)
                .ToListAsync();

            var result = Assert.Single(savedResults);

            Assert.Equal(sku, result.SKU);
            Assert.Equal(100, result.ShopifyQuantity);
            Assert.Equal(90, result.WarehouseQuantity);
            Assert.Equal(10, result.Difference);
            Assert.Equal("Mismatch", result.Status);
            Assert.Equal("Medium", result.Severity);
        }
        finally
        {
            await using var cleanup =
                TestDatabase.CreateContext();

            await cleanup.AuditRuns
                .Where(x =>
                    x.OrganizationId == organizationId &&
                    x.IdempotencyKey == key)
                .ExecuteDeleteAsync();
        }
    }
}
