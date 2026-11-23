
using System.Globalization;
using StockGuard.Core.Models;

namespace StockGuard.ConsoleApp.Services;

public class CsvInventoryService
{
    // Load inventory records from a CSV file
    public List<InventoryRecord> LoadInventory(string fileName)
    {
        List<InventoryRecord> inventory = new();

        string filePath = Path.Combine(
            AppContext.BaseDirectory,
            fileName);

        string[] lines = File.ReadAllLines(filePath);

        // Skip blank lines at the beginning
        int headerIndex = 0;

        while (headerIndex < lines.Length &&
               string.IsNullOrWhiteSpace(lines[headerIndex]))
        {
            headerIndex++;
        }

        if (headerIndex >= lines.Length)
        {
            throw new FormatException(
                "The CSV file is empty or contains only blank lines.");
        }

        // Validate CSV header
        string header = lines[headerIndex]
            .Trim()
            .Trim('\uFEFF');

        if (!header.Equals(
            "SKU,Quantity",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new FormatException(
                "Invalid CSV header. Expected: SKU,Quantity");
        }

        // Track duplicate SKUs
        HashSet<string> seenSkus = new(
            StringComparer.OrdinalIgnoreCase);

        // Read inventory rows
        for (int i = headerIndex + 1; i < lines.Length; i++)
        {
            string line = lines[i].Trim();

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] fields = line.Split(',');

            if (fields.Length != 2)
            {
                throw new FormatException(
                    $"Invalid CSV format on line {i + 1}.");
            }

            string sku = fields[0].Trim();
            string quantityText = fields[1].Trim();

            if (string.IsNullOrWhiteSpace(sku))
            {
                throw new FormatException(
                    $"Missing SKU on line {i + 1}.");
            }

            if (!int.TryParse(
                quantityText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int quantity))
            {
                throw new FormatException(
                    $"Invalid quantity for SKU: {sku}");
            }

            if (quantity < 0)
            {
                throw new FormatException(
                    $"Negative quantity for SKU: {sku}");
            }

            if (!seenSkus.Add(sku))
            {
                throw new FormatException(
                    $"Duplicate SKU found: {sku}");
            }

            inventory.Add(new InventoryRecord
            {
                SKU = sku,
                Quantity = quantity
            });
        }

        return inventory;
    }

    // Export audit results with severity levels
    public string ExportReport(List<AuditResult> results)
    {
        string reportPath = Path.Combine(
            AppContext.BaseDirectory,
            "StockGuard_Report.csv");

        using StreamWriter writer = new(reportPath);

        // CSV headers
        writer.WriteLine(
            "SKU,ShopifyQuantity,WarehouseQuantity,Status,Difference,Severity");

        // Export each audit result
        foreach (AuditResult result in results)
        {
            writer.WriteLine(
                $"{result.SKU}," +
                $"{result.ShopifyQuantity}," +
                $"{result.WarehouseQuantity}," +
                $"{result.Status}," +
                $"{result.Difference}," +
                $"{result.Severity}");
        }

        return reportPath;
    }
}

