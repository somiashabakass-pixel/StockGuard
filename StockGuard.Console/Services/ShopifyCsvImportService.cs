
using System.Globalization;
using StockGuard.ConsoleApp.Models;

namespace StockGuard.ConsoleApp.Services;

public class ShopifyCsvImportService
{
    public List<InventoryRecord> LoadInventory(string fileName)
    {
        List<InventoryRecord> inventory = new();

        string filePath = Path.Combine(
            AppContext.BaseDirectory,
            fileName);

        using StreamReader reader = new(filePath);

        // STEP 1: Skip blank lines before the CSV header
        string? headerLine;

        do
        {
            headerLine = reader.ReadLine();
        }
        while (headerLine is not null &&
               string.IsNullOrWhiteSpace(headerLine));

        if (headerLine is null)
        {
            throw new FormatException(
                "The CSV file is empty or contains only blank lines.");
        }

        // STEP 2: Read and clean CSV headers
        string[] headers = ParseCsvLine(headerLine)
            .Select(header => header.Trim().Trim('\uFEFF'))
            .ToArray();

        // Temporary debugging information
        

        // STEP 3: Find required Shopify columns
        int skuIndex = Array.FindIndex(
            headers,
            header => header.Equals(
                "SKU",
                StringComparison.OrdinalIgnoreCase));

        int quantityIndex = Array.FindIndex(
            headers,
            header => header.Equals(
                "On hand (current)",
                StringComparison.OrdinalIgnoreCase));

        if (skuIndex < 0 || quantityIndex < 0)
        {
            throw new FormatException(
                "The CSV file must contain 'SKU' and 'On hand (current)' columns.");
        }

        // STEP 4: Read inventory rows
        while (reader.ReadLine() is string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            string[] fields = ParseCsvLine(line);

            string sku = skuIndex < fields.Length
                ? fields[skuIndex].Trim()
                : "";

            string quantityText = quantityIndex < fields.Length
                ? fields[quantityIndex].Trim()
                : "";

            // Skip rows without a SKU
            if (string.IsNullOrWhiteSpace(sku))
            {
                continue;
            }

            // Validate inventory quantity
            if (!int.TryParse(
                quantityText,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int quantity))
            {
                throw new FormatException(
                    $"Invalid quantity for SKU: {sku}");
            }

            // Add inventory record
            inventory.Add(new InventoryRecord
            {
                SKU = sku,
                Quantity = quantity
            });
        }

        return inventory;
    }

    // Parses CSV fields, including quoted commas
    private static string[] ParseCsvLine(string line)
    {
        List<string> fields = new();
        System.Text.StringBuilder field = new();

        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char character = line[i];

            if (character == '"')
            {
                if (inQuotes &&
                    i + 1 < line.Length &&
                    line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        fields.Add(field.ToString());

        return fields.ToArray();
    }
}

