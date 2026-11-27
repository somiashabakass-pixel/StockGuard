using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using StockGuard.Api.Dtos;
using StockGuard.Api.Helpers;
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
    private readonly ILogger<AuditsController> _logger;

    public AuditsController(
        InventoryAuditService auditService,
        InventoryRiskService riskService,
        AuditPersistenceService persistenceService,
        IWebHostEnvironment environment,
        ILogger<AuditsController> logger)
    {
        _auditService = auditService;
        _riskService = riskService;
        _persistenceService = persistenceService;
        _environment = environment;
        _logger = logger;
    }

    [HttpPost("compare")]
    [RequestSizeLimit(10_000_000)]
    public ActionResult<AuditResponseDto> Compare(
        [FromBody] AuditRequestDto request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();

        var comparison = ExecuteAudit(request, cancellationToken);

        if (comparison.Result is OkObjectResult ok &&
            ok.Value is AuditResponseDto response)
        {
            stopwatch.Stop();

            LogAudit(
                "JSON comparison",
                response,
                stopwatch.Elapsed.TotalMilliseconds);
        }

        return comparison;
    }

    [HttpPost("save")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<AuditResponseDto>> Save(
        [FromBody] AuditRequestDto request, CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();

        if (!IdempotencyHeader.TryRead(
            Request.Headers,
            out string? idempotencyKey,
            out string? headerError))
        {
            ModelState.AddModelError(
                IdempotencyHeader.HeaderName,
                headerError!);

            return ValidationProblem(ModelState);
        }

        DateTime startedAtUtc = DateTime.UtcNow;

        if (!TryPrepareInventory(
                request,
                cancellationToken,
                out var shopifyInventory,
                out var warehouseInventory))
        {
            return ValidationProblem(ModelState);
        }

        string? requestHash = null;

        if (idempotencyKey is not null)
        {
            requestHash = AuditRequestHashService.ComputeHash(
                DevelopmentDataService.OrganizationId,
                DevelopmentDataService.StoreId,
                DevelopmentDataService.WarehouseId,
                shopifyInventory,
                warehouseInventory);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (idempotencyKey is not null)
        {
            try
            {
                var existing =
                    await _persistenceService.TryGetExistingAuditAsync(
                        DevelopmentDataService.OrganizationId,
                        DevelopmentDataService.StoreId,
                        DevelopmentDataService.WarehouseId,
                        idempotencyKey,
                        requestHash!,
                        cancellationToken);

                if (existing is not null)
                {
                    var replayResponse =
                        AuditResponseMapper.FromPersistedSnapshot(
                            existing);

                    stopwatch.Stop();

                    LogAudit(
                        "JSON audit replay",
                        replayResponse,
                        stopwatch.Elapsed.TotalMilliseconds);

                    return Ok(replayResponse);
                }
            }
            catch (IdempotencyConflictException)
            {
                return Problem(
                    statusCode: StatusCodes.Status409Conflict,
                    title: "Idempotency conflict",
                    detail: "The idempotency key has already been used with a different audit request.");
            }
        }

        if (!TryCalculateAudit(
                shopifyInventory,
                warehouseInventory,
                cancellationToken,
                out var results,
                out var risk))
        {
            return ValidationProblem(ModelState);
        }



        cancellationToken.ThrowIfCancellationRequested();

        AuditSaveResult savedAudit;

        try
        {
            savedAudit =
                await _persistenceService.SaveAuditWithDetailsAsync(
                    DevelopmentDataService.OrganizationId,
                    DevelopmentDataService.StoreId,
                    DevelopmentDataService.WarehouseId,
                    startedAtUtc,
                    results,
                    risk,
                    cancellationToken,
                    idempotencyKey: idempotencyKey,
                    requestHash: requestHash);
        }
        catch (IdempotencyConflictException)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Idempotency conflict",
                detail: "The idempotency key has already been used with a different audit request.");
        }


PersistedAuditSnapshot snapshot =
    await _persistenceService.GetPersistedAuditAsync(
        DevelopmentDataService.OrganizationId,
        savedAudit.AuditId,
        cancellationToken)
    ?? throw new InvalidOperationException(
        "The saved audit could not be retrieved.");

        AuditResponseDto response = AuditResponseMapper.FromPersistedSnapshot(snapshot);


        stopwatch.Stop();

        LogAudit(
            "JSON saved audit",
            response,
            stopwatch.Elapsed.TotalMilliseconds);

        return Ok(response);
    }

    private void LogAudit(
        string operation,
        AuditResponseDto response,
        double durationMs)
    {
        _logger.LogInformation(
            "{Operation} completed. AuditId: {AuditId}, Products: {TotalProducts}, Matching: {MatchingProducts}, Mismatched: {MismatchedProducts}, Missing: {MissingProducts}, Discrepancy: {TotalUnitsDiscrepancy}, DurationMs: {DurationMs}",
            operation,
            response.AuditId,
            response.TotalProducts,
            response.MatchingProducts,
            response.MismatchedProducts,
            response.MissingProducts,
            response.TotalUnitsDiscrepancy,
            durationMs);
    }

    private bool TryPrepareInventory(
        AuditRequestDto request,
        CancellationToken cancellationToken,
        out List<InventoryRecord> shopifyInventory,
        out List<InventoryRecord> warehouseInventory)
    {
        cancellationToken.ThrowIfCancellationRequested();

        shopifyInventory = new();
        warehouseInventory = new();

        ValidateInventory(
            request.ShopifyInventory,
            nameof(request.ShopifyInventory),
            cancellationToken);

        ValidateInventory(
            request.WarehouseInventory,
            nameof(request.WarehouseInventory),
            cancellationToken);

        if (!ModelState.IsValid)
        {
            return false;
        }

        shopifyInventory = request.ShopifyInventory
            .Select(x => new InventoryRecord
            {
                SKU = x.SKU.Trim().ToUpperInvariant(),
                Quantity = x.Quantity!.Value
            }).ToList();

        warehouseInventory = request.WarehouseInventory
            .Select(x => new InventoryRecord
            {
                SKU = x.SKU.Trim().ToUpperInvariant(),
                Quantity = x.Quantity!.Value
            }).ToList();

        cancellationToken.ThrowIfCancellationRequested();

        return true;
    }

    private bool TryCalculateAudit(
        List<InventoryRecord> shopifyInventory,
        List<InventoryRecord> warehouseInventory,
        CancellationToken cancellationToken,
        out List<AuditResult> results,
        out InventoryRiskSummary risk)
    {
        cancellationToken.ThrowIfCancellationRequested();

        risk = new();

        results = _auditService.CompareInventory(
            shopifyInventory,
            warehouseInventory,
            cancellationToken);

        long totalDiscrepancy = results
            .Where(x => x.Status == "Mismatch")
            .Sum(x => Math.Abs((long)(x.Difference ?? 0)));

        if (totalDiscrepancy > int.MaxValue)
        {
            ModelState.AddModelError(
                "Inventory",
                "Total discrepancy exceeds the supported limit.");

            return false;
        }

        risk = _riskService.CalculateRisk(
            results,
            cancellationToken);

        return true;
    }

    private ActionResult<AuditResponseDto> ExecuteAudit(
        AuditRequestDto request,
        CancellationToken cancellationToken)
    {
        if (!TryPrepareInventory(
                request,
                cancellationToken,
                out var shopifyInventory,
                out var warehouseInventory))
        {
            return ValidationProblem(ModelState);
        }

        if (!TryCalculateAudit(
                shopifyInventory,
                warehouseInventory,
                cancellationToken,
                out var results,
                out var risk))
        {
            return ValidationProblem(ModelState);
        }

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
        string fieldName, CancellationToken cancellationToken)
    {
        HashSet<string> seenSkus =
            new(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < inventory.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

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
                    "Duplicate SKU detected.");
            }
        }
    }
}
