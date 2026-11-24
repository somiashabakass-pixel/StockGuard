using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Services;

// STOCKGUARD AUDIT - VERSION 1.0
// Inventory Reconciliation + Permanent Audit Storage

try
{
    // STEP 1: Connect to PostgreSQL
    DatabaseConnectionService databaseService = new();

    bool databaseConnected =
        await databaseService.TestConnectionAsync();

    if (!databaseConnected)
    {
        throw new InvalidOperationException(
            "Unable to connect to PostgreSQL.");
    }

    Console.WriteLine("PostgreSQL connection successful!");

    // STEP 2: Initialize local development data
    DevelopmentDataService developmentDataService = new();

    await developmentDataService.InitializeAsync();

    Console.WriteLine("Development database initialized!");
    Console.WriteLine();

    // STEP 3: Create services
    CsvInventoryService csvService = new();
    ShopifyCsvImportService shopifyService = new();
    InventoryAuditService auditService = new();
    InventoryRiskService riskService = new();
    AuditPersistenceService persistenceService = new();

    // STEP 4: Record audit start time
    DateTime startedAtUtc = DateTime.UtcNow;

    // STEP 5: Load inventory
    List<InventoryRecord> shopifyInventory =
        shopifyService.LoadInventory("shopify-export-sample.csv");

    List<InventoryRecord> warehouseInventory =
        csvService.LoadInventory("warehouse.csv");

    // STEP 6: Compare inventory
    List<AuditResult> results =
        auditService.CompareInventory(
            shopifyInventory,
            warehouseInventory);

    // STEP 7: Calculate risk summary
    InventoryRiskSummary riskSummary =
        riskService.CalculateRisk(results);

    // STEP 8: Permanently save audit
    Guid auditId = await persistenceService.SaveAuditAsync(
        DevelopmentDataService.OrganizationId,
        DevelopmentDataService.StoreId,
        DevelopmentDataService.WarehouseId,
        startedAtUtc,
        results,
        riskSummary);

    Console.WriteLine("Audit saved to PostgreSQL!");
    Console.WriteLine($"Audit ID: {auditId}");
    Console.WriteLine();

    // STEP 9: Display audit results
    Console.WriteLine("==================================");
    Console.WriteLine("       STOCKGUARD AUDIT v1.0      ");
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

    // STEP 10: Export report
    string reportPath = csvService.ExportReport(results);

    // STEP 11: Calculate audit statistics
    int matches = results.Count(
        result => result.Status == "Match");

    int mismatches = results.Count(
        result => result.Status == "Mismatch");

    int missingProducts = results.Count(
        result => result.Status.StartsWith("Missing"));

    // STEP 12: Display audit summary
    Console.WriteLine("----------------------------------");
    Console.WriteLine("            AUDIT SUMMARY         ");
    Console.WriteLine("----------------------------------");

    Console.WriteLine($"Total unique products: {results.Count}");
    Console.WriteLine($"Matches: {matches}");
    Console.WriteLine($"Quantity mismatches: {mismatches}");
    Console.WriteLine($"Missing products: {missingProducts}");

    // STEP 13: Display inventory risk summary
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
    Console.WriteLine($"Database audit ID: {auditId}");
    Console.WriteLine($"Report saved to: {reportPath}");
}
catch (Exception ex)
{
    Console.WriteLine($"Audit failed: {ex.Message}");
    Environment.ExitCode = 1;
}
