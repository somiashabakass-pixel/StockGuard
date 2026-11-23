
using StockGuard.ConsoleApp.Models;

namespace StockGuard.ConsoleApp.Services;

public class InventoryRiskService
{
    public InventoryRiskSummary CalculateRisk(
        List<AuditResult> results)
    {
        InventoryRiskSummary summary = new();

        foreach (AuditResult result in results)
        {
            // Count products by severity
            switch (result.Severity)
            {
                case "High":
                    summary.HighSeverity++;
                    break;

                case "Medium":
                    summary.MediumSeverity++;
                    break;

                case "Low":
                    summary.LowSeverity++;
                    break;

                case "Missing":
                    summary.MissingProducts++;
                    break;

                case "None":
                    summary.MatchingProducts++;
                    break;
            }

            // Count mismatched units
            if (result.Status == "Mismatch" &&
                result.Difference.HasValue)
            {
                summary.TotalUnitsDiscrepancy +=
                    Math.Abs(result.Difference.Value);
            }
        }

        return summary;
    }
}
