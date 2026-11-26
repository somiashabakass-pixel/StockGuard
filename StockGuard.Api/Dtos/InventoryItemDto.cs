using System.ComponentModel.DataAnnotations;

namespace StockGuard.Api.Dtos;

public class InventoryItemDto
{
    [Required]
    [StringLength(255)]
    public string SKU { get; set; } = string.Empty;

    [Required]
    [Range(0, int.MaxValue)]
    public int? Quantity { get; set; }
}
