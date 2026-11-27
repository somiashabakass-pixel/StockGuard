using Microsoft.AspNetCore.Mvc;
using StockGuard.Api.Dtos;
using StockGuard.Infrastructure.Data;

namespace StockGuard.Api.Controllers;

[ApiController]
[Route("api/audits/history")]
public class AuditHistoryController : ControllerBase
{
    private readonly AuditHistoryService _historyService;
    private readonly IWebHostEnvironment _environment;

    public AuditHistoryController(
        AuditHistoryService historyService,
        IWebHostEnvironment environment)
    {
        _historyService = historyService;
        _environment = environment;
    }

    [HttpGet]
    public async Task<ActionResult<List<AuditHistoryDto>>> GetHistory(
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        if (limit < 1 || limit > 100)
        {
            ModelState.AddModelError(
                "limit",
                "Limit must be between 1 and 100.");

            return ValidationProblem(ModelState);
        }

        cancellationToken.ThrowIfCancellationRequested();

        var audits =
            await _historyService.GetAuditHistoryAsync(
                DevelopmentDataService.OrganizationId,
                limit,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        var response = audits.Select(x =>
            new AuditHistoryDto
            {
                AuditId = x.Id,
                StartedAtUtc = x.StartedAtUtc,
                CompletedAtUtc = x.CompletedAtUtc,
                Status = x.Status,
                TotalProducts = x.TotalProducts,
                MatchingProducts = x.MatchingProducts,
                MismatchedProducts = x.MismatchedProducts,
                MissingProducts = x.MissingProducts,
                TotalUnitsDiscrepancy =
                    x.TotalUnitsDiscrepancy
            }).ToList();

        return Ok(response);
    }

    [HttpGet("{auditId:guid}/results")]
    public async Task<ActionResult<List<AuditResultDto>>> GetResults(
        Guid auditId,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment() && !_environment.IsEnvironment("Testing"))
        {
            return NotFound();
        }

        cancellationToken.ThrowIfCancellationRequested();

        var results =
            await _historyService.GetAuditResultsAsync(
                DevelopmentDataService.OrganizationId,
                auditId,
                cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (results.Count == 0)
        {
            return NotFound();
        }

        var response = results.Select(x =>
            new AuditResultDto
            {
                SKU = x.SKU,
                ShopifyQuantity = x.ShopifyQuantity,
                WarehouseQuantity = x.WarehouseQuantity,
                Difference = x.Difference,
                Status = x.Status,
                Severity = x.Severity
            }).ToList();

        return Ok(response);
    }
}
