using Microsoft.AspNetCore.Mvc;
using StockGuard.Api.Dtos;
using StockGuard.Core.Models;
using StockGuard.Core.Services;
using StockGuard.Infrastructure.Data;

namespace StockGuard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditsController : ControllerBase
{
    private readonly InventoryAuditService _auditService;
    private readonly InventoryRiskService _riskService;
    private readonly AuditPersistenceService _persistenceService;
    private readonly IWebHostEnvironment _environment;

    public AuditsController(
        InventoryAuditService auditService,
        InventoryRiskService riskService,
        AuditPersistenceService persistenceService,
        IWebHostEnvironment environment)
    {
        _auditService = auditService;
        _riskService = riskService;
        _persistenceService = persistenceService;
        _environment = environment;
    }

    [HttpPost("compare")]
    [RequestSizeLimit(10_000_000)]
    public ActionResult<AuditResponseDto> Compare(
        [FromBody] AuditRequestDto request)
    {
        return ExecuteAudit(request);
    }

    [HttpPost("save")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<AuditResponseDto>> Save(
        [FromBody] AuditRequestDto request)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        DateTime startedAtUtc = DateTime.UtcNow;

        var comparison = ExecuteAudit(request);

        if (comparison.Result is not OkObjectResult ok ||
            ok.Value is not AuditResponseDto response)
        {
            return comparison;
        }

        List<AuditResult> results =
            response.Results.Select(x => new AuditResult
            {
                SKU = x.SKU,
                ShopifyQuantity = x.ShopifyQuantity,
                WarehouseQuantity = x.WarehouseQuantity,
                Difference = x.Difference,
                Status = x.Status,
                Severity = x.Severity
            }).ToList();

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

        response.AuditId = auditId;
        response.CompletedAtUtc = DateTime.UtcNow;

        return Ok(response);
    }

    private ActionResult<AuditResponseDto> ExecuteAudit(
        AuditRequestDto request)
    {
        ValidateInventory(
            request.ShopifyInventory,
            nameof(request.ShopifyInventory));

        ValidateInventory(
            request.WarehouseInventory,
            nameof(request.WarehouseInventory));

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        List<InventoryRecord> shopifyInventory =
            request.ShopifyInventory.Select(x =>
                new InventoryRecord
                {
                    SKU = x.SKU.Trim().ToUpperInvariant(),
                    Quantity = x.Quantity!.Value
                }).ToList();

        List<InventoryRecord> warehouseInventory =
            request.WarehouseInventory.Select(x =>
                new InventoryRecord
                {
                    SKU = x.SKU.Trim().ToUpperInvariant(),
                    Quantity = x.Quantity!.Value
                }).ToList();

        List<AuditResult> results =
            _auditService.CompareInventory(
                shopifyInventory,
                warehouseInventory);

        long totalDiscrepancy = results
            .Where(x => x.Status == "Mismatch")
            .Sum(x => Math.Abs((long)(x.Difference ?? 0)));

        if (totalDiscrepancy > int.MaxValue)
        {
            ModelState.AddModelError(
                "Inventory",
                "Total discrepancy exceeds the supported limit.");

            return ValidationProblem(ModelState);
        }

        InventoryRiskSummary risk =
            _riskService.CalculateRisk(results);

        AuditResponseDto response = new()
        {
            AuditId = null,
            CompletedAtUtc = DateTime.UtcNow,
            TotalProducts = results.Count,
            MatchingProducts = risk.MatchingProducts,
            MismatchedProducts =
                results.Count(x => x.Status == "Mismatch"),
            MissingProducts = risk.MissingProducts,
            HighSeverity = risk.HighSeverity,
            MediumSeverity = risk.MediumSeverity,
            LowSeverity = risk.LowSeverity,
            TotalUnitsDiscrepancy =
                risk.TotalUnitsDiscrepancy,

            Results = results.Select(x =>
                new AuditResultDto
                {
                    SKU = x.SKU,
                    ShopifyQuantity = x.ShopifyQuantity,
                    WarehouseQuantity = x.WarehouseQuantity,
                    Difference = x.Difference,
                    Status = x.Status,
                    Severity = x.Severity
                }).ToList()
        };

        return Ok(response);
    }

    private void ValidateInventory(
        List<InventoryItemDto> inventory,
        string fieldName)
    {
        HashSet<string> seenSkus =
            new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < inventory.Count; i++)
        {
            var item = inventory[i];

            if (item is null)
            {
                ModelState.AddModelError(
                    $"{fieldName}[{i}]",
                    "Inventory item cannot be null.");

                continue;
            }

            if (string.IsNullOrWhiteSpace(item.SKU))
            {
                ModelState.AddModelError(
                    $"{fieldName}[{i}].SKU",
                    "SKU cannot be empty.");

                continue;
            }

            if (!item.Quantity.HasValue ||
                item.Quantity.Value < 0)
            {
                ModelState.AddModelError(
                    $"{fieldName}[{i}].Quantity",
                    "A nonnegative quantity is required.");
            }

            if (!seenSkus.Add(item.SKU.Trim()))
            {
                ModelState.AddModelError(
                    $"{fieldName}[{i}].SKU",
                    $"Duplicate SKU: {item.SKU}");
            }
        }
    }
}
