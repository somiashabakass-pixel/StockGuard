using StockGuard.Core.Models;

namespace StockGuard.Core.Services;

public class InventoryAuditService
{
    public virtual List<AuditResult> CompareInventory(
        List<InventoryRecord> shopifyInventory,
        List<InventoryRecord> warehouseInventory,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        List<AuditResult> results = new();

        Dictionary<string, int> shopify = new();
        Dictionary<string, int> warehouse = new();

        foreach (var item in shopifyInventory)
        {
            cancellationToken.ThrowIfCancellationRequested();

            shopify.Add(item.SKU, item.Quantity);
        }

        foreach (var item in warehouseInventory)
        {
            cancellationToken.ThrowIfCancellationRequested();

            warehouse.Add(item.SKU, item.Quantity);
        }

        HashSet<string> skuSet = new();

        foreach (string sku in shopify.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            skuSet.Add(sku);
        }

        foreach (string sku in warehouse.Keys)
        {
            cancellationToken.ThrowIfCancellationRequested();

            skuSet.Add(sku);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var allSkus = skuSet
            .OrderBy(sku => sku)
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();

        foreach (string sku in allSkus)
        {
            cancellationToken.ThrowIfCancellationRequested();

            bool inShopify = shopify.TryGetValue(
                sku, out int shopifyQuantity);

            bool inWarehouse = warehouse.TryGetValue(
                sku, out int warehouseQuantity);

            AuditResult result = new()
            {
                SKU = sku,

                ShopifyQuantity = inShopify
                    ? shopifyQuantity : null,

                WarehouseQuantity = inWarehouse
                    ? warehouseQuantity : null
            };

            if (!inShopify)
            {
                result.Status = "Missing from Shopify";
                result.Severity = "Missing";
            }
            else if (!inWarehouse)
            {
                result.Status = "Missing from Warehouse";
                result.Severity = "Missing";
            }
            else
            {
                result.Difference =
                    shopifyQuantity - warehouseQuantity;

                int absoluteDifference =
                    Math.Abs(result.Difference.Value);

                if (absoluteDifference == 0)
                {
                    result.Status = "Match";
                    result.Severity = "None";
                }
                else if (absoluteDifference <= 5)
                {
                    result.Status = "Mismatch";
                    result.Severity = "Low";
                }
                else if (absoluteDifference <= 20)
                {
                    result.Status = "Mismatch";
                    result.Severity = "Medium";
                }
                else
                {
                    result.Status = "Mismatch";
                    result.Severity = "High";
                }
            }

            results.Add(result);
        }

        cancellationToken.ThrowIfCancellationRequested();

        return results;
    }
}
