
using StockGuard.ConsoleApp.Models;

namespace StockGuard.ConsoleApp.Services;

public class InventoryAuditService
{
    public List<AuditResult> CompareInventory(
        List<InventoryRecord> shopifyInventory,
        List<InventoryRecord> warehouseInventory)
    {
        List<AuditResult> results = new();

        var shopify = shopifyInventory.ToDictionary(
            item => item.SKU,
            item => item.Quantity);

        var warehouse = warehouseInventory.ToDictionary(
            item => item.SKU,
            item => item.Quantity);

        var allSkus = shopify.Keys
            .Union(warehouse.Keys)
            .OrderBy(sku => sku);

        foreach (string sku in allSkus)
        {
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

        return results;
    }
}
