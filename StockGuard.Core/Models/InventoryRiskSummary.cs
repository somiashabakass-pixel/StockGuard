namespace StockGuard.Core.Models;

public class InventoryRiskSummary
{
    public int HighSeverity { get; set; }
    public int MediumSeverity { get; set; }
    public int LowSeverity { get; set; }
    public int MissingProducts { get; set; }
    public int MatchingProducts { get; set; }
    public int TotalUnitsDiscrepancy { get; set; }
}