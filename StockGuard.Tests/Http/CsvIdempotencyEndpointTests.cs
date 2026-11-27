using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data;
using StockGuard.Tests.Integration;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class CsvIdempotencyEndpointTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly HttpClient _client;

    public CsvIdempotencyEndpointTests(
        StockGuardApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("SameRequest", 1)]
    [InlineData("ConflictingRequest", 1)]
    [InlineData("DifferentKeys", 2)]
    [InlineData("NoKey", 2)]
    [InlineData("ConcurrentRequests", 1)]
    [InlineData("InvalidKey", 0)]
    [InlineData("MultipleKeys", 0)]
    public async Task Upload_ShouldEnforceIdempotency(
        string scenario,
        int expectedCount)
    {
        await HttpTestData.InitializeAsync();

        string sku = $"CSV-IDEMPOTENCY-{Guid.NewGuid():N}"
            .ToUpperInvariant();

        string key = Guid.NewGuid().ToString("N");

        async Task<HttpResponseMessage> SendAsync(
            string? requestKey,
            int warehouseQuantity = 90,
            bool multipleKeys = false)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/audits/upload");

            using var form = new MultipartFormDataContent();

            string shopifyCsv =
                "SKU,Location,On hand (current)\n" +
                $"{sku},Main Warehouse,100\n";

            string warehouseCsv =
                "SKU,Quantity\n" +
                $"{sku},{warehouseQuantity}\n";

            form.Add(
                new StringContent(
                    shopifyCsv, Encoding.UTF8, "text/csv"),
                "ShopifyFile",
                "shopify.csv");

            form.Add(
                new StringContent(
                    warehouseCsv, Encoding.UTF8, "text/csv"),
                "WarehouseFile",
                "warehouse.csv");

            request.Content = form;

            if (requestKey is not null)
            {
                if (multipleKeys)
                {
                    request.Headers.TryAddWithoutValidation(
                        "Idempotency-Key",
                        new[] { requestKey, "second-key" });
                }
                else
                {
                    request.Headers.TryAddWithoutValidation(
                        "Idempotency-Key",
                        requestKey);
                }
            }

            return await _client.SendAsync(request);
        }

        async Task<(Guid Id, DateTime CompletedAt)>
            ReadAuditAsync(HttpResponseMessage response)
        {
            Assert.Equal(
                HttpStatusCode.OK,
                response.StatusCode);

            var body = await response.Content
                .ReadFromJsonAsync<JsonElement>();

            return (
                body.GetProperty("auditId").GetGuid(),
                body.GetProperty("completedAtUtc").GetDateTime()
            );
        }

        try
        {
            switch (scenario)
            {
                case "SameRequest":
                {
                    using var first = await SendAsync(key);
                    using var second = await SendAsync(key);

                    Assert.Equal(
                        await ReadAuditAsync(first),
                        await ReadAuditAsync(second));

                    break;
                }

                case "ConflictingRequest":
                {
                    using var first = await SendAsync(key);
                    await ReadAuditAsync(first);

                    using var second = await SendAsync(key, 80);

                    Assert.Equal(
                        HttpStatusCode.Conflict,
                        second.StatusCode);

                    Assert.Equal(
                        "application/problem+json",
                        second.Content.Headers.ContentType?.MediaType);

                    break;
                }

                case "DifferentKeys":
                {
                    using var first = await SendAsync(key);

                    using var second = await SendAsync(
                        Guid.NewGuid().ToString("N"));

                    Assert.NotEqual(
                        (await ReadAuditAsync(first)).Id,
                        (await ReadAuditAsync(second)).Id);

                    break;
                }

                case "NoKey":
                {
                    using var first = await SendAsync(null);
                    using var second = await SendAsync(null);

                    Assert.NotEqual(
                        (await ReadAuditAsync(first)).Id,
                        (await ReadAuditAsync(second)).Id);

                    break;
                }

                case "ConcurrentRequests":
                {
                    var responses = await Task.WhenAll(
                        Enumerable.Range(0, 5)
                            .Select(_ => SendAsync(key)));

                    try
                    {
                        var audits =
                            new List<(Guid, DateTime)>();

                        System.Text.Json.Nodes.JsonNode? expectedBody = null;

                        foreach (var response in responses)
                        {
                            Assert.Equal(
                                HttpStatusCode.OK,
                                response.StatusCode);

                            var actualBody =
                                System.Text.Json.Nodes.JsonNode.Parse(
                                    await response.Content.ReadAsStringAsync());

                            Assert.NotNull(actualBody);

                            if (expectedBody is null)
                            {
                                expectedBody = actualBody;
                            }
                            else
                            {
                                Assert.True(
                                    System.Text.Json.Nodes.JsonNode.DeepEquals(
                                        expectedBody, actualBody),
                                    "Concurrent audit responses differ.");
                            }

                            audits.Add(
                                await ReadAuditAsync(response));
                        }

                        Assert.Single(audits.Distinct());
                    }
                    finally
                    {
                        foreach (var response in responses)
                        {
                            response.Dispose();
                        }
                    }

                    break;
                }

                case "InvalidKey":
                {
                    using var response = await SendAsync(
                        new string('A', 129));

                    Assert.Equal(
                        HttpStatusCode.BadRequest,
                        response.StatusCode);

                    break;
                }

                case "MultipleKeys":
                {
                    using var response = await SendAsync(
                        key, multipleKeys: true);

                    Assert.Equal(
                        HttpStatusCode.BadRequest,
                        response.StatusCode);

                    break;
                }

                default:
                    throw new InvalidOperationException(
                        "Unknown test scenario.");
            }

            await using var verification =
                TestDatabase.CreateContext();

            var auditIds = await verification.AuditResults
                .Where(x =>
                    x.SKU == sku &&
                    x.AuditRun.OrganizationId ==
                        DevelopmentDataService.OrganizationId)
                .Select(x => x.AuditRunId)
                .Distinct()
                .ToListAsync();

            Assert.Equal(expectedCount, auditIds.Count);

            int resultCount = await verification.AuditResults
                .CountAsync(x =>
                    x.SKU == sku &&
                    x.AuditRun.OrganizationId ==
                        DevelopmentDataService.OrganizationId);

            Assert.Equal(expectedCount, resultCount);
        }
        finally
        {
            await using var cleanup =
                TestDatabase.CreateContext();

            var auditIds = await cleanup.AuditResults
                .Where(x =>
                    x.SKU == sku &&
                    x.AuditRun.OrganizationId ==
                        DevelopmentDataService.OrganizationId)
                .Select(x => x.AuditRunId)
                .Distinct()
                .ToListAsync();

            await cleanup.AuditRuns
                .Where(x =>
                    auditIds.Contains(x.Id) &&
                    x.OrganizationId ==
                        DevelopmentDataService.OrganizationId)
                .ExecuteDeleteAsync();
        }
    }
}
