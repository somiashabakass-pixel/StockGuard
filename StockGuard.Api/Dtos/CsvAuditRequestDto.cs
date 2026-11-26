using System.ComponentModel.DataAnnotations;

namespace StockGuard.Api.Dtos;

public class CsvAuditRequestDto
{
    [Required]
    public IFormFile ShopifyFile { get; set; } = null!;

    [Required]
    public IFormFile WarehouseFile { get; set; } = null!;
}
