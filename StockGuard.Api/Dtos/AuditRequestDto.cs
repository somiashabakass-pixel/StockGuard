using System.ComponentModel.DataAnnotations;

namespace StockGuard.Api.Dtos;

public class AuditRequestDto
{
    [Required]
    [MinLength(1)]
    [MaxLength(10000)]
    public List<InventoryItemDto> ShopifyInventory
        { get; set; } = new();

    [Required]
    [MinLength(1)]
    [MaxLength(10000)]
    public List<InventoryItemDto> WarehouseInventory
        { get; set; } = new();
}
