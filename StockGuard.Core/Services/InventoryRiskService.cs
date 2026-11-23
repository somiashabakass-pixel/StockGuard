using StockGuard.Core.Models;

namespace StockGuard.Core.Services;

public class InventoryRiskService
{
    public InventoryRiskSummary CalculateRisk(
        List<AuditResult> results)
    {
        InventoryRiskSummary summary = new();

        foreach (AuditResult result in results)
        {
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