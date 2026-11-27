using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Tests.Integration;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class CalculationBypassTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public CalculationBypassTests(
        StockGuardApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class CountingAuditService
        : InventoryAuditService
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public override List<AuditResult> CompareInventory(
            List<InventoryRecord> shopifyInventory,
            List<InventoryRecord> warehouseInventory,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);

            return base.CompareInventory(
                shopifyInventory,
                warehouseInventory,
                cancellationToken);
        }
    }

    private sealed class CountingRiskService
        : InventoryRiskService
    {
        private int _calls;

        public int Calls => Volatile.Read(ref _calls);

        public override InventoryRiskSummary CalculateRisk(
            List<AuditResult> results,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _calls);

            return base.CalculateRisk(
                results,
                cancellationToken);
        }
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task MatchingRetries_ShouldBypassCalculations(
        bool originalJson,
        bool retryJson)
    {
        await HttpTestData.InitializeAsync();

        var auditCounter = new CountingAuditService();
        var riskCounter = new CountingRiskService();

        using var factory = _factory.WithWebHostBuilder(
            builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    services.RemoveAll<InventoryAuditService>();
                    services.RemoveAll<InventoryRiskService>();

                    services.AddSingleton<InventoryAuditService>(
                        auditCounter);

                    services.AddSingleton<InventoryRiskService>(
                        riskCounter);
                });
            });

        using var client = factory.CreateClient();

        string sku =
            $"BYPASS-{Guid.NewGuid():N}".ToUpperInvariant();

        string key = Guid.NewGuid().ToString("N");

        Guid organizationId =
            DevelopmentDataService.OrganizationId;

        async Task<HttpResponseMessage> SendAsync(
            bool useJson,
            int warehouseQuantity = 90)
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

            return await client.SendAsync(request);
        }

        try
        {
            using var original =
                await SendAsync(originalJson);

            Assert.Equal(
                HttpStatusCode.OK,
                original.StatusCode);

            var originalBody = JsonNode.Parse(
                await original.Content.ReadAsStringAsync());

            Assert.NotNull(originalBody);

            Assert.Equal(1, auditCounter.Calls);
            Assert.Equal(1, riskCounter.Calls);

            for (int i = 0; i < 4; i++)
            {
                using var retry =
                    await SendAsync(retryJson);

                Assert.Equal(
                    HttpStatusCode.OK,
                    retry.StatusCode);

                var retryBody = JsonNode.Parse(
                    await retry.Content.ReadAsStringAsync());

                Assert.True(
                    JsonNode.DeepEquals(
                        originalBody,
                        retryBody));

                Assert.Equal(1, auditCounter.Calls);
                Assert.Equal(1, riskCounter.Calls);
            }

            using var conflict =
                await SendAsync(retryJson, 80);

            Assert.Equal(
                HttpStatusCode.Conflict,
                conflict.StatusCode);

            Assert.Equal(1, auditCounter.Calls);
            Assert.Equal(1, riskCounter.Calls);

            await using var verification =
                TestDatabase.CreateContext();

            var saved = Assert.Single(
                await verification.AuditRuns
                    .AsNoTracking()
                    .Where(x =>
                        x.OrganizationId == organizationId &&
                        x.IdempotencyKey == key)
                    .ToListAsync());

            Assert.Equal(
                originalBody["auditId"]!.GetValue<Guid>(),
                saved.Id);

            Assert.Equal(
                1,
                await verification.AuditResults
                    .CountAsync(x =>
                        x.AuditRunId == saved.Id));
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