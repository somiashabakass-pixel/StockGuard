using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StockGuard.Infrastructure.Data;
using StockGuard.Tests.Integration;
using Xunit;

namespace StockGuard.Tests.Http;

[Trait("Category", "Integration")]
public class CsvPersistenceEndpointTests
    : IClassFixture<StockGuardApiFactory>
{
    private readonly StockGuardApiFactory _factory;

    public CsvPersistenceEndpointTests(
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

    [Fact]
    public async Task Upload_ValidCsv_PersistsAuditAndResults()
    {
        await HttpTestData.InitializeAsync();

        string prefix = $"HTTP-{Guid.NewGuid():N}".ToUpperInvariant();

        Guid? auditId = null;

        try
        {
            using var client = _factory.CreateClient();
            using var form = new MultipartFormDataContent();

            string shopify =
                "SKU,Location,On hand (current)\n" +
                $"{prefix}-001,Main Warehouse,100\n" +
                $"{prefix}-002,Main Warehouse,50\n" +
                $"{prefix}-003,Main Warehouse,75\n";

            string warehouse =
                "SKU,Quantity\n" +
                $"{prefix}-001,92\n" +
                $"{prefix}-002,50\n" +
                $"{prefix}-003,60\n" +
                $"{prefix}-004,40\n";

            AddFile(form, "ShopifyFile",
                "shopify.csv", shopify);

            AddFile(form, "WarehouseFile",
                "warehouse.csv", warehouse);

            using var response = await client.PostAsync(
                "/api/audits/upload", form);

            string body =
                await response.Content.ReadAsStringAsync();

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                body);

            using var document = JsonDocument.Parse(body);

            var root = document.RootElement;

            auditId = root.GetProperty("auditId").GetGuid();

            Assert.NotEqual(Guid.Empty, auditId.Value);

            Assert.Equal(4,
                root.GetProperty("totalProducts").GetInt32());

            Assert.Equal(1,
                root.GetProperty("matchingProducts").GetInt32());

            Assert.Equal(2,
                root.GetProperty("mismatchedProducts").GetInt32());

            Assert.Equal(1,
                root.GetProperty("missingProducts").GetInt32());

            Assert.Equal(23,
                root.GetProperty("totalUnitsDiscrepancy").GetInt32());

            DateTime apiTimestamp =
                root.GetProperty("completedAtUtc").GetDateTime();

            await using var context =
                TestDatabase.CreateContext();

            var savedAudit =
                await context.AuditRuns
                    .AsNoTracking()
                    .SingleAsync(x => x.Id == auditId.Value);

            Assert.Equal(
                DevelopmentDataService.OrganizationId,
                savedAudit.OrganizationId);

            Assert.Equal(
                DevelopmentDataService.StoreId,
                savedAudit.StoreId);

            Assert.Equal(
                DevelopmentDataService.WarehouseId,
                savedAudit.WarehouseId);

            Assert.Equal("Completed", savedAudit.Status);
            Assert.Equal(4, savedAudit.TotalProducts);
            Assert.Equal(1, savedAudit.MatchingProducts);
            Assert.Equal(2, savedAudit.MismatchedProducts);
            Assert.Equal(1, savedAudit.MissingProducts);
            Assert.Equal(23, savedAudit.TotalUnitsDiscrepancy);

            Assert.Equal(
                apiTimestamp,
                savedAudit.CompletedAtUtc!.Value);

            Assert.Equal(
                DateTimeKind.Utc,
                savedAudit.CompletedAtUtc.Value.Kind);

            Assert.True(
                savedAudit.CompletedAtUtc.Value >=
                savedAudit.StartedAtUtc);

            var results = await context.AuditResults
                .AsNoTracking()
                .Where(x => x.AuditRunId == auditId.Value)
                .ToListAsync();

            Assert.Equal(4, results.Count);

            var mismatch = Assert.Single(
                results, x => x.SKU == $"{prefix}-001");

            Assert.Equal(100, mismatch.ShopifyQuantity);
            Assert.Equal(92, mismatch.WarehouseQuantity);
            Assert.Equal(8, mismatch.Difference);
            Assert.Equal("Mismatch", mismatch.Status);
            Assert.Equal("Medium", mismatch.Severity);
        }
        finally
        {
            await using var cleanup =
                TestDatabase.CreateContext();

            var ownedAuditIds = await cleanup.AuditResults
                .Where(x =>
                    x.SKU.StartsWith(prefix + "-") &&
                    x.AuditRun.OrganizationId ==
                        DevelopmentDataService.OrganizationId)
                .Select(x => x.AuditRunId)
                .Distinct()
                .ToListAsync();

            await cleanup.AuditRuns
                .Where(x =>
                    ownedAuditIds.Contains(x.Id) &&
                    x.OrganizationId ==
                        DevelopmentDataService.OrganizationId)
                .ExecuteDeleteAsync();
        }
    }
}
