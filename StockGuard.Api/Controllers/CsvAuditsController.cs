using Microsoft.AspNetCore.Mvc;
using StockGuard.Api.Dtos;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Services;
using StockGuard.Infrastructure.Data;

namespace StockGuard.Api.Controllers;

[ApiController]
[Route("api/audits")]
public class CsvAuditsController : ControllerBase
{
    private readonly CsvUploadService _csvService;
    private readonly InventoryAuditService _auditService;
    private readonly InventoryRiskService _riskService;
    private readonly AuditPersistenceService _persistenceService;
    private readonly IWebHostEnvironment _environment;

    public CsvAuditsController(
        CsvUploadService csvService,
        InventoryAuditService auditService,
        InventoryRiskService riskService,
        AuditPersistenceService persistenceService,
        IWebHostEnvironment environment)
    {
        _csvService = csvService;
        _auditService = auditService;
        _riskService = riskService;
        _persistenceService = persistenceService;
        _environment = environment;
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [RequestFormLimits(
        MultipartBodyLengthLimit = 5 * 1024 * 1024)]
    public async Task<ActionResult<AuditResponseDto>> Upload(
        [FromForm] CsvAuditRequestDto request)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        DateTime startedAtUtc = DateTime.UtcNow;

        ValidateFile(
            request.ShopifyFile,
            nameof(request.ShopifyFile));

        ValidateFile(
            request.WarehouseFile,
            nameof(request.WarehouseFile));

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        List<InventoryRecord> shopifyInventory;
        List<InventoryRecord> warehouseInventory;

        try
        {
            using var shopifyStream =
                request.ShopifyFile.OpenReadStream();

            shopifyInventory =
                _csvService.ReadShopifyInventory(
                    shopifyStream);
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(
                nameof(request.ShopifyFile),
                exception.Message);

            return ValidationProblem(ModelState);
        }

        try
        {
            using var warehouseStream =
                request.WarehouseFile.OpenReadStream();

            warehouseInventory =
                _csvService.ReadWarehouseInventory(
                    warehouseStream);
        }
        catch (InvalidDataException exception)
        {
            ModelState.AddModelError(
                nameof(request.WarehouseFile),
                exception.Message);

            return ValidationProblem(ModelState);
        }

        List<AuditResult> results =
            _auditService.CompareInventory(
                shopifyInventory,
                warehouseInventory);

        long totalDiscrepancy = results
            .Where(x => x.Status == "Mismatch")
            .Sum(x => Math.Abs(
                (long)(x.Difference ?? 0)));

        if (totalDiscrepancy > int.MaxValue)
        {
            ModelState.AddModelError(
                "Inventory",
                "Total discrepancy exceeds the supported limit.");

            return ValidationProblem(ModelState);
        }

        InventoryRiskSummary risk =
            _riskService.CalculateRisk(results);

        Guid auditId =
            await _persistenceService.SaveAuditAsync(
                DevelopmentDataService.OrganizationId,
                DevelopmentDataService.StoreId,
                DevelopmentDataService.WarehouseId,
                startedAtUtc,
                results,
                risk);

        AuditResponseDto response = new()
        {
            AuditId = auditId,

            CompletedAtUtc = DateTime.UtcNow,

            TotalProducts = results.Count,

            MatchingProducts =
                risk.MatchingProducts,

            MismatchedProducts = results.Count(
                x => x.Status == "Mismatch"),

            MissingProducts =
                risk.MissingProducts,

            HighSeverity =
                risk.HighSeverity,

            MediumSeverity =
                risk.MediumSeverity,

            LowSeverity =
                risk.LowSeverity,

            TotalUnitsDiscrepancy =
                risk.TotalUnitsDiscrepancy,

            Results = results.Select(x =>
                new AuditResultDto
                {
                    SKU = x.SKU,

                    ShopifyQuantity =
                        x.ShopifyQuantity,

                    WarehouseQuantity =
                        x.WarehouseQuantity,

                    Difference = x.Difference,

                    Status = x.Status,

                    Severity = x.Severity
                }).ToList()
        };

        return Ok(response);
    }

    private void ValidateFile(
        IFormFile? file,
        string fieldName)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError(
                fieldName,
                "A nonempty CSV file is required.");

            return;
        }

        if (file.Length > CsvUploadService.MaxFileSize)
        {
            ModelState.AddModelError(
                fieldName,
                "The file exceeds the 5 MB limit.");
        }

        if (!string.Equals(
            Path.GetExtension(file.FileName),
            ".csv",
            StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(
                fieldName,
                "Only CSV files are supported.");
        }
    }
}
