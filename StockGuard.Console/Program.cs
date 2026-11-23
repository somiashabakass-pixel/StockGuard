
using StockGuard.ConsoleApp.Models;
using StockGuard.ConsoleApp.Services;

// STOCKGUARD AUDIT - VERSION 0.9
// Inventory Reconciliation + Severity + Risk Summary

try
{
    // STEP 1: Create services
    CsvInventoryService csvService = new();
    ShopifyCsvImportService shopifyService = new();
    InventoryAuditService auditService = new();
    InventoryRiskService riskService = new();

    // STEP 2: Load inventory
    List<InventoryRecord> shopifyInventory =
        shopifyService.LoadInventory("shopify-export-sample.csv");

    List<InventoryRecord> warehouseInventory =
        csvService.LoadInventory("warehouse.csv");

    // STEP 3: Compare inventory
    List<AuditResult> results =
        auditService.CompareInventory(
            shopifyInventory,
            warehouseInventory);

    // STEP 4: Calculate risk summary
    InventoryRiskSummary riskSummary =
        riskService.CalculateRisk(results);

    // STEP 5: Display audit results
    Console.WriteLine("==================================");
    Console.WriteLine("       STOCKGUARD AUDIT v0.9      ");
    Console.WriteLine("==================================");

    foreach (AuditResult result in results)
    {
        Console.WriteLine(
            $"{result.SKU}: {result.Status.ToUpperInvariant()}" +
            (result.Status == "Mismatch"
                ? $" | Difference: {result.Difference}"
                : "") +
            $" | Severity: {result.Severity.ToUpperInvariant()}");
    }

    // STEP 6: Export report
    string reportPath = csvService.ExportReport(results);

    // STEP 7: Calculate audit statistics
    int matches = results.Count(
        result => result.Status == "Match");

    int mismatches = results.Count(
        result => result.Status == "Mismatch");

    int missingProducts = results.Count(
        result => result.Status.StartsWith("Missing"));

    // STEP 8: Display audit summary
    Console.WriteLine("----------------------------------");
    Console.WriteLine("            AUDIT SUMMARY         ");
    Console.WriteLine("----------------------------------");

    Console.WriteLine($"Total unique products: {results.Count}");
    Console.WriteLine($"Matches: {matches}");
    Console.WriteLine($"Quantity mismatches: {mismatches}");
    Console.WriteLine($"Missing products: {missingProducts}");

    // STEP 9: Display inventory risk summary
    Console.WriteLine("----------------------------------");
    Console.WriteLine("        INVENTORY RISK SUMMARY    ");
    Console.WriteLine("----------------------------------");

    Console.WriteLine($"High severity: {riskSummary.HighSeverity}");
    Console.WriteLine($"Medium severity: {riskSummary.MediumSeverity}");
    Console.WriteLine($"Low severity: {riskSummary.LowSeverity}");
    Console.WriteLine($"Missing products: {riskSummary.MissingProducts}");
    Console.WriteLine($"Matching products: {riskSummary.MatchingProducts}");
    Console.WriteLine(
        $"Total units discrepancy: {riskSummary.TotalUnitsDiscrepancy}");

    Console.WriteLine("----------------------------------");
    Console.WriteLine("Audit completed successfully!");
    Console.WriteLine($"Report saved to: {reportPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"Audit failed: {ex.Message}");
    Environment.ExitCode = 1;
}

