
using StockGuard.ConsoleApp.Models;
using StockGuard.ConsoleApp.Services;
using Xunit;

namespace StockGuard.Tests;

public class InventoryRiskServiceTests
{
    [Fact]
    public void CalculateRisk_CountsAllSeverityLevels()
    {
        InventoryRiskService service = new();

        List<AuditResult> results = new()
        {
            new AuditResult
            {
                SKU = "SKU-001",
                Status = "Mismatch",
                Severity = "High",
                Difference = 25
            },
            new AuditResult
            {
                SKU = "SKU-002",
                Status = "Mismatch",
                Severity = "Medium",
                Difference = -10
            },
            new AuditResult
            {
                SKU = "SKU-003",
                Status = "Mismatch",
                Severity = "Low",
                Difference = 3
            },
            new AuditResult
            {
                SKU = "SKU-004",
                Status = "Missing from Warehouse",
                Severity = "Missing"
            },
            new AuditResult
            {
                SKU = "SKU-005",
                Status = "Match",
                Severity = "None",
                Difference = 0
            }
        };

        InventoryRiskSummary summary =
            service.CalculateRisk(results);

        Assert.Equal(1, summary.HighSeverity);
        Assert.Equal(1, summary.MediumSeverity);
        Assert.Equal(1, summary.LowSeverity);
        Assert.Equal(1, summary.MissingProducts);
        Assert.Equal(1, summary.MatchingProducts);
        Assert.Equal(38, summary.TotalUnitsDiscrepancy);
    }

    [Fact]
    public void CalculateRisk_EmptyResultsReturnsZero()
    {
        InventoryRiskService service = new();

        List<AuditResult> results = new();

        InventoryRiskSummary summary =
            service.CalculateRisk(results);

        Assert.Equal(0, summary.HighSeverity);
        Assert.Equal(0, summary.MediumSeverity);
        Assert.Equal(0, summary.LowSeverity);
        Assert.Equal(0, summary.MissingProducts);
        Assert.Equal(0, summary.MatchingProducts);
        Assert.Equal(0, summary.TotalUnitsDiscrepancy);
    }
}
