using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;
using StockGuard.Infrastructure.Data.Entities;
using StockGuard.Infrastructure.Services;

// STOCKGUARD AUDIT - VERSION 1.1
// Permanent Storage + Historical Audit Retrieval

try
{
    DatabaseConnectionService databaseService = new();

    bool databaseConnected =
        await databaseService.TestConnectionAsync();

    if (!databaseConnected)
    {
        throw new InvalidOperationException(
            "Unable to connect to PostgreSQL.");
    }

    Console.WriteLine("PostgreSQL connection successful!");

    DevelopmentDataService developmentDataService = new();
    await developmentDataService.InitializeAsync();

    Console.WriteLine("Development database initialized!");
    Console.WriteLine();

    AuditHistoryService historyService = new();

    // HISTORY MODE
    if (args.Contains("--history"))
    {
        List<AuditRun> history =
            await historyService.GetAuditHistoryAsync(
                DevelopmentDataService.OrganizationId);

        Console.WriteLine("==================================");
        Console.WriteLine("       STOCKGUARD AUDIT HISTORY   ");
        Console.WriteLine("==================================");

        Console.WriteLine($"Audits retrieved: {history.Count}");

        foreach (AuditRun audit in history)
        {
            Console.WriteLine("----------------------------------");
            Console.WriteLine($"Audit ID: {audit.Id}");
            Console.WriteLine($"Started: {audit.StartedAtUtc:u}");
            Console.WriteLine($"Status: {audit.Status}");
            Console.WriteLine($"Total products: {audit.TotalProducts}");
            Console.WriteLine($"Matches: {audit.MatchingProducts}");
            Console.WriteLine($"Mismatches: {audit.MismatchedProducts}");
            Console.WriteLine($"Missing: {audit.MissingProducts}");
            Console.WriteLine(
                $"Units discrepancy: {audit.TotalUnitsDiscrepancy}");
        }

        if (history.Count > 0)
        {
            AuditRun latestAudit = history[0];

            List<AuditResultEntity> savedResults =
                await historyService.GetAuditResultsAsync(
                    DevelopmentDataService.OrganizationId,
                    latestAudit.Id);

            Console.WriteLine("==================================");
            Console.WriteLine("       LATEST SAVED AUDIT         ");
            Console.WriteLine("==================================");

            foreach (AuditResultEntity result in savedResults)
            {
                Console.WriteLine(
                    $"{result.SKU} | " +
                    $"Shopify: {result.ShopifyQuantity?.ToString() ?? "N/A"} | " +
                    $"Warehouse: {result.WarehouseQuantity?.ToString() ?? "N/A"} | " +
                    $"Difference: {result.Difference?.ToString() ?? "N/A"} | " +
                    $"{result.Status} | {result.Severity}");
            }
        }

        return;
    }

    // NORMAL AUDIT MODE
    CsvInventoryService csvService = new();
    ShopifyCsvImportService shopifyService = new();
    InventoryAuditService auditService = new();
    InventoryRiskService riskService = new();
    AuditPersistenceService persistenceService = new();

    DateTime startedAtUtc = DateTime.UtcNow;

    List<InventoryRecord> shopifyInventory =
        shopifyService.LoadInventory("shopify-export-sample.csv");

    List<InventoryRecord> warehouseInventory =
        csvService.LoadInventory("warehouse.csv");

    List<AuditResult> results =
        auditService.CompareInventory(
            shopifyInventory,
            warehouseInventory);

    InventoryRiskSummary riskSummary =
        riskService.CalculateRisk(results);

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

    Console.WriteLine("==================================");
    Console.WriteLine("       STOCKGUARD AUDIT v1.1      ");
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

    string reportPath = csvService.ExportReport(results);

    int matches = results.Count(
        result => result.Status == "Match");

    int mismatches = results.Count(
        result => result.Status == "Mismatch");

    int missingProducts = results.Count(
        result => result.Status.StartsWith("Missing"));

    Console.WriteLine("----------------------------------");
    Console.WriteLine("            AUDIT SUMMARY         ");
    Console.WriteLine("----------------------------------");

    Console.WriteLine($"Total unique products: {results.Count}");
    Console.WriteLine($"Matches: {matches}");
    Console.WriteLine($"Quantity mismatches: {mismatches}");
    Console.WriteLine($"Missing products: {missingProducts}");

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
    Console.WriteLine($"Operation failed: {ex.Message}");
    Environment.ExitCode = 1;
}
