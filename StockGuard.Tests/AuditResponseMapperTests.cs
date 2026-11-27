using StockGuard.Api.Helpers;
using StockGuard.Core.Models;
using StockGuard.Infrastructure.Data;
using Xunit;

namespace StockGuard.Tests;

public class AuditResponseMapperTests
{
    private static PersistedAuditSnapshot CreateSnapshot()
    {
        return new PersistedAuditSnapshot(
            Guid.NewGuid(),
            new DateTime(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc),
            3,
            1,
            1,
            1,
            1,
            1,
            0,
            15,
            new List<AuditResult>
            {
                new()
                {
                    SKU = "SKU-001",
                    ShopifyQuantity = 25,
                    WarehouseQuantity = 10,
                    Difference = 15,
                    Status = "Mismatch",
                    Severity = "High"
                },
                new()
                {
                    SKU = "SKU-002",
                    ShopifyQuantity = 10,
                    WarehouseQuantity = 10,
                    Difference = 0,
                    Status = "Match",
                    Severity = "None"
                },
                new()
                {
                    SKU = "SKU-003",
                    ShopifyQuantity = 5,
                    WarehouseQuantity = null,
                    Difference = null,
                    Status = "Missing",
                    Severity = "Medium"
                }
            });
    }

    [Fact]
    public void Mapper_PreservesAuditId()
    {
        var snapshot = CreateSnapshot();
        var response = AuditResponseMapper.FromPersistedSnapshot(snapshot);

        Assert.Equal(snapshot.AuditId, response.AuditId);
    }

    [Fact]
    public void Mapper_PreservesCompletionTimestamp()
    {
        var snapshot = CreateSnapshot();
        var response = AuditResponseMapper.FromPersistedSnapshot(snapshot);

        Assert.Equal(snapshot.CompletedAtUtc, response.CompletedAtUtc);
    }

    [Fact]
    public void Mapper_PreservesSummaryCounts()
    {
        var snapshot = CreateSnapshot();
        var response = AuditResponseMapper.FromPersistedSnapshot(snapshot);

        Assert.Equal(3, response.TotalProducts);
        Assert.Equal(1, response.MatchingProducts);
        Assert.Equal(1, response.MismatchedProducts);
        Assert.Equal(1, response.MissingProducts);
        Assert.Equal(15, response.TotalUnitsDiscrepancy);
    }

    [Fact]
    public void Mapper_PreservesSeverityCounts()
    {
        var snapshot = CreateSnapshot();
        var response = AuditResponseMapper.FromPersistedSnapshot(snapshot);

        Assert.Equal(1, response.HighSeverity);
        Assert.Equal(1, response.MediumSeverity);
        Assert.Equal(0, response.LowSeverity);
    }

    [Fact]
    public void Mapper_PreservesIndividualResults()
    {
        var snapshot = CreateSnapshot();
        var response = AuditResponseMapper.FromPersistedSnapshot(snapshot);

        Assert.Equal(snapshot.Results.Count, response.Results.Count);

        for (int i = 0; i < snapshot.Results.Count; i++)
        {
            var expected = snapshot.Results[i];
            var actual = response.Results[i];

            Assert.Equal(expected.SKU, actual.SKU);
            Assert.Equal(expected.ShopifyQuantity, actual.ShopifyQuantity);
            Assert.Equal(expected.WarehouseQuantity, actual.WarehouseQuantity);
            Assert.Equal(expected.Difference, actual.Difference);
            Assert.Equal(expected.Status, actual.Status);
            Assert.Equal(expected.Severity, actual.Severity);
        }
    }

    [Fact]
    public void Mapper_HandlesEmptyResults()
    {
        var snapshot = new PersistedAuditSnapshot(
            Guid.NewGuid(),
            DateTime.UtcNow,
            0, 0, 0, 0, 0, 0, 0, 0,
            Array.Empty<AuditResult>());

        var response = AuditResponseMapper.FromPersistedSnapshot(snapshot);

        Assert.Empty(response.Results);
        Assert.Equal(0, response.TotalProducts);
        Assert.Equal(0, response.TotalUnitsDiscrepancy);
    }

    [Fact]
    public void Mapper_RejectsNullSnapshot()
    {
        Assert.Throws<ArgumentNullException>(
            () => AuditResponseMapper.FromPersistedSnapshot(null!));
    }
}
