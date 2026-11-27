using System.Security.Cryptography;
using System.Text.Json;
using StockGuard.Core.Models;

namespace StockGuard.Core.Services;

public static class AuditRequestHashService
{
    public static string ComputeHash(
        Guid organizationId,
        Guid storeId,
        Guid warehouseId,
        IEnumerable<InventoryRecord> shopifyInventory,
        IEnumerable<InventoryRecord> warehouseInventory)
    {
        ArgumentNullException.ThrowIfNull(shopifyInventory);
        ArgumentNullException.ThrowIfNull(warehouseInventory);

        var payload = new
        {
            Version = 1,
            OrganizationId = organizationId,
            StoreId = storeId,
            WarehouseId = warehouseId,
            ShopifyInventory = Normalize(shopifyInventory),
            WarehouseInventory = Normalize(warehouseInventory)
        };

        byte[] data = JsonSerializer.SerializeToUtf8Bytes(payload);

        byte[] hash = SHA256.HashData(data);

        return Convert.ToHexString(hash);
    }

    private static object Normalize(
        IEnumerable<InventoryRecord> inventory)
    {
        return inventory
            .Select(item => new
            {
                SKU = item.SKU.Trim().ToUpperInvariant(),
                item.Quantity
            })
            .OrderBy(item => item.SKU, StringComparer.Ordinal)
            .ThenBy(item => item.Quantity)
            .ToArray();
    }
}
