using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using StockGuard.Tests.Integration;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class JsonIdempotencyEndpointTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly HttpClient _client;

    public JsonIdempotencyEndpointTests(
        StockGuardApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("SameRequest")]
    [InlineData("ConflictingRequest")]
    [InlineData("DifferentKeys")]
    [InlineData("NoKey")]
    [InlineData("ConcurrentRequests")]
    [InlineData("InvalidKey")]
    [InlineData("MultipleKeys")]
    public async Task Save_ShouldEnforceIdempotency(
        string scenario)
    {
        await HttpTestData.InitializeAsync();

        string sku =
            $"IDEMPOTENCY-{Guid.NewGuid():N}"
                .ToUpperInvariant();

        string key = Guid.NewGuid().ToString("N");

        async Task<HttpResponseMessage> SendAsync(
            string? requestKey,
            int warehouseQuantity = 90,
            bool multipleKeys = false)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/audits/save");

            request.Content = JsonContent.Create(new
            {
                shopifyInventory = new[]
                {
                    new { sku, quantity = 100 }
                },
                warehouseInventory = new[]
                {
                    new { sku, quantity = warehouseQuantity }
                }
            });

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

        async Task<Guid> ReadAuditIdAsync(
            HttpResponseMessage response)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content
                .ReadFromJsonAsync<System.Text.Json.JsonElement>();

            return body.GetProperty("auditId").GetGuid();
        }

        int expectedCount = 1;

        try
        {
            switch (scenario)
            {
                case "SameRequest":
                {
                    using var first = await SendAsync(key);
                    using var second = await SendAsync(key);

                    Assert.Equal(
                        await ReadAuditIdAsync(first),
                        await ReadAuditIdAsync(second));

                    break;
                }

                case "ConflictingRequest":
                {
                    using var first = await SendAsync(key);
                    await ReadAuditIdAsync(first);

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
                    expectedCount = 2;

                    using var first = await SendAsync(key);

                    using var second = await SendAsync(
                        Guid.NewGuid().ToString("N"));

                    Assert.NotEqual(
                        await ReadAuditIdAsync(first),
                        await ReadAuditIdAsync(second));

                    break;
                }

                case "NoKey":
                {
                    expectedCount = 2;

                    using var first = await SendAsync(null);
                    using var second = await SendAsync(null);

                    Assert.NotEqual(
                        await ReadAuditIdAsync(first),
                        await ReadAuditIdAsync(second));

                    break;
                }

                case "ConcurrentRequests":
                {
                    var tasks = Enumerable.Range(0, 5)
                        .Select(_ => SendAsync(key))
                        .ToArray();

                    var responses = await Task.WhenAll(tasks);

                    try
                    {
                        var ids = new List<Guid>();

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

                            ids.Add(
                                await ReadAuditIdAsync(response));
                        }

                        Assert.Single(ids.Distinct());
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
                    expectedCount = 0;

                    using var response = await SendAsync(
                        new string('A', 129));

                    Assert.Equal(
                        HttpStatusCode.BadRequest,
                        response.StatusCode);

                    break;
                }

                case "MultipleKeys":
                {
                    expectedCount = 0;

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
                .Where(x => x.SKU == sku)
                .Select(x => x.AuditRunId)
                .Distinct()
                .ToListAsync();

            Assert.Equal(expectedCount, auditIds.Count);

            int resultCount = await verification.AuditResults
                .CountAsync(x => x.SKU == sku);

            Assert.Equal(expectedCount, resultCount);
        }
        finally
        {
            await using var cleanup =
                TestDatabase.CreateContext();

            var auditIds = await cleanup.AuditResults
                .Where(x => x.SKU == sku)
                .Select(x => x.AuditRunId)
                .Distinct()
                .ToListAsync();

            await cleanup.AuditRuns
                .Where(x => auditIds.Contains(x.Id))
                .ExecuteDeleteAsync();
        }
    }
}
