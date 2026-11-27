using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data;
using StockGuard.Tests.Integration;
using Xunit;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class PersistedResponseReplayTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly HttpClient _client;

    public PersistedResponseReplayTests(
        StockGuardApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Retry_ShouldReturnPersistedResults(
        bool firstIsJson,
        bool retryIsJson)
    {
        await HttpTestData.InitializeAsync();

        string sku = $"REPLAY-{Guid.NewGuid():N}"
            .ToUpperInvariant();

        string key = Guid.NewGuid().ToString("N");

        Guid organizationId =
            DevelopmentDataService.OrganizationId;

        async Task<JsonNode> SendAsync(bool useJson)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                useJson
                    ? "/api/audits/save"
                    : "/api/audits/upload");

            request.Headers.Add("Idempotency-Key", key);

            if (useJson)
            {
                request.Content = JsonContent.Create(new
                {
                    shopifyInventory = new[]
                    {
                        new { sku, quantity = 100 }
                    },
                    warehouseInventory = new[]
                    {
                        new { sku, quantity = 90 }
                    }
                });
            }
            else
            {
                var form = new MultipartFormDataContent();

                form.Add(
                    new StringContent(
                        "SKU,Location,On hand (current)\n" +
                        $"{sku},Main Warehouse,100\n",
                        Encoding.UTF8,
                        "text/csv"),
                    "ShopifyFile",
                    "shopify.csv");

                form.Add(
                    new StringContent(
                        "SKU,Quantity\n" +
                        $"{sku},90\n",
                        Encoding.UTF8,
                        "text/csv"),
                    "WarehouseFile",
                    "warehouse.csv");

                request.Content = form;
            }

            using var response =
                await _client.SendAsync(request);

            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            return JsonNode.Parse(
                await response.Content.ReadAsStringAsync())!;
        }

        try
        {
            var first = await SendAsync(firstIsJson);

            Guid auditId =
                first["auditId"]!.GetValue<Guid>();

            Assert.NotEqual(Guid.Empty, auditId);

            Assert.Equal(
                "Medium",
                first["results"]![0]!["severity"]!
                    .GetValue<string>());

            // Deliberately change a synthetic test result.
            // This distinguishes database replay from
            // freshly calculated inventory results.
            await using (var db = TestDatabase.CreateContext())
            {
                int affected = await db.AuditResults
                    .Where(x =>
                        x.AuditRunId == auditId &&
                        x.SKU == sku &&
                        x.AuditRun.OrganizationId ==
                            organizationId)
                    .ExecuteUpdateAsync(setters =>
                        setters.SetProperty(
                            x => x.Severity, "High"));

                Assert.Equal(1, affected);
            }

            var retry = await SendAsync(retryIsJson);

            Assert.Equal(
                auditId,
                retry["auditId"]!.GetValue<Guid>());

            Assert.Equal(
                first["completedAtUtc"]!.GetValue<DateTime>(),
                retry["completedAtUtc"]!.GetValue<DateTime>());

            Assert.Equal(
                "High",
                retry["results"]![0]!["severity"]!
                    .GetValue<string>());

            var expected = first.DeepClone();

            expected["results"]![0]!["severity"] = "High";
            expected["highSeverity"] = 1;
            expected["mediumSeverity"] = 0;

            Assert.True(
                JsonNode.DeepEquals(expected, retry),
                "Retry did not return the persisted snapshot.");

            await using var verification =
                TestDatabase.CreateContext();

            int auditCount = await verification.AuditRuns
                .CountAsync(x =>
                    x.OrganizationId == organizationId &&
                    x.IdempotencyKey == key);

            Assert.Equal(1, auditCount);

            int resultCount = await verification.AuditResults
                .CountAsync(x =>
                    x.AuditRunId == auditId);

            Assert.Equal(1, resultCount);
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
