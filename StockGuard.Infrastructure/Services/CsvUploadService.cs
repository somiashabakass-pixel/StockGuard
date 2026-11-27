using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using StockGuard.Core.Models;

namespace StockGuard.Infrastructure.Services;

public class CsvUploadService
{
    public const int MaxRecords = 10000;
    public const int MaxFileSize = 5 * 1024 * 1024;

    public List<InventoryRecord> ReadShopifyInventory(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        return ReadInventory(
            stream,
            "On hand (current)",
            true,
            cancellationToken);
    }

    public List<InventoryRecord> ReadWarehouseInventory(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        return ReadInventory(
            stream,
            "Quantity",
            false,
            cancellationToken);
    }

    private List<InventoryRecord> ReadInventory(
        Stream stream,
        string quantityColumn,
        bool requireLocation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stream);

        cancellationToken.ThrowIfCancellationRequested();

        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new InvalidDataException(
                "The uploaded file must be readable and seekable.");
        }

        if (stream.Length > MaxFileSize)
        {
            throw new InvalidDataException(
                "The CSV file exceeds the 5 MB limit.");
        }

        var configuration = new CsvConfiguration(
            CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            IgnoreBlankLines = true,
            TrimOptions = TrimOptions.Trim,
            DetectColumnCountChanges = true,
            ExceptionMessagesContainRawData = false,
            MaxFieldSize = 65536,
            PrepareHeaderForMatch = args =>
                args.Header.Trim().ToUpperInvariant()
        };

        using var reader = new StreamReader(
            stream,
            new UTF8Encoding(true, true),
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 4096,
            leaveOpen: true);

        using var csv = new CsvReader(reader, configuration);

        List<InventoryRecord> inventory = new();

        HashSet<string> seenSkus =
            new(StringComparer.OrdinalIgnoreCase);

        string? inventoryLocation = null;

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!csv.Read())
            {
                throw new InvalidDataException(
                    "The CSV file is empty.");
            }

            csv.ReadHeader();

            cancellationToken.ThrowIfCancellationRequested();

            var headers = csv.HeaderRecord
                ?? throw new InvalidDataException(
                    "CSV headers are missing.");

            var normalizedHeaders = headers
                .Select(x => x.Trim().ToUpperInvariant())
                .ToList();

            if (headers.Any(header => header.Any(char.IsControl)))
            {
                throw new InvalidDataException(
                    "CSV column headers contain invalid control characters.");
            }

            if (headers.Any(header => header.Length > 65536))
            {
                throw new InvalidDataException(
                    "CSV column header exceeds the maximum allowed length.");
            }

            if (normalizedHeaders.Any(string.IsNullOrWhiteSpace))
            {
                throw new InvalidDataException(
                    "CSV column headers cannot be empty.");
            }

            if (normalizedHeaders.Distinct().Count()
                != normalizedHeaders.Count)
            {
                throw new InvalidDataException(
                    "Duplicate CSV headers were found.");
            }

            if (!normalizedHeaders.Contains("SKU"))
            {
                throw new InvalidDataException(
                    "The required SKU column is missing.");
            }

            if (!normalizedHeaders.Contains(
                quantityColumn.ToUpperInvariant()))
            {
                throw new InvalidDataException(
                    $"Required column '{quantityColumn}' is missing.");
            }

            if (requireLocation &&
                !normalizedHeaders.Contains("LOCATION"))
            {
                throw new InvalidDataException(
                    "The Shopify Location column is required.");
            }

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!csv.Read())
                {
                    break;
                }

                cancellationToken.ThrowIfCancellationRequested();

                int row = csv.Parser.Row;

                for (int column = 0;
                     column < csv.Parser.Count;
                     column++)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    string field = csv.GetField(column)
                        ?? string.Empty;

                    if (field.Length > 65536)
                    {
                        throw new InvalidDataException(
                            $"Row {row}: CSV field exceeds the maximum allowed length.");
                    }
                }

                if (inventory.Count >= MaxRecords)
                {
                    throw new InvalidDataException(
                        "The CSV exceeds 10,000 inventory records.");
                }

                if (requireLocation)
                {
                    string location =
                        csv.GetField("Location")?.Trim()
                        ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(location))
                    {
                        throw new InvalidDataException(
                            $"Row {row}: Location cannot be empty.");
                    }

                    inventoryLocation ??= location;

                    if (!string.Equals(
                        inventoryLocation,
                        location,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidDataException(
                            "Shopify CSV must contain only one inventory location.");
                    }
                }

                string sku =
                    csv.GetField("SKU")?.Trim()
                    ?? string.Empty;

                if (string.IsNullOrWhiteSpace(sku))
                {
                    throw new InvalidDataException(
                        $"Row {row}: SKU cannot be empty.");
                }

                if (sku.Length > 255)
                {
                    throw new InvalidDataException(
                        $"Row {row}: SKU exceeds 255 characters.");
                }

                if (sku.Any(char.IsControl))
                {
                    throw new InvalidDataException(
                        $"Row {row}: SKU contains invalid characters.");
                }

                sku = sku.ToUpperInvariant();

                if (!seenSkus.Add(sku))
                {
                    throw new InvalidDataException(
                        $"Row {row}: Duplicate SKU detected.");
                }

                string quantityText =
                    csv.GetField(quantityColumn)
                    ?? string.Empty;

                if (!int.TryParse(
                    quantityText,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out int quantity)
                    || quantity < 0)
                {
                    throw new InvalidDataException(
                        $"Row {row}: Invalid inventory quantity.");
                }

                inventory.Add(new InventoryRecord
                {
                    SKU = sku,
                    Quantity = quantity
                });
            }
        }
        catch (CsvHelperException exception)
        {
            throw new InvalidDataException(
                "The CSV file contains invalid formatting.",
                exception);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException(
                "The CSV file contains invalid text encoding.",
                exception);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (inventory.Count == 0)
        {
            throw new InvalidDataException(
                "The CSV contains no inventory records.");
        }

        return inventory;
    }
}
