
using StockGuard.Api.Dtos;
using StockGuard.Infrastructure.Data;

namespace StockGuard.Api.Helpers;

public static class AuditResponseMapper
{
    public static AuditResponseDto FromPersistedSnapshot(
        PersistedAuditSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new AuditResponseDto
        {
            AuditId = snapshot.AuditId,

            CompletedAtUtc = snapshot.CompletedAtUtc,

            TotalProducts = snapshot.TotalProducts,

            MatchingProducts = snapshot.MatchingProducts,

            MismatchedProducts = snapshot.MismatchedProducts,

            MissingProducts = snapshot.MissingProducts,

            HighSeverity = snapshot.HighSeverity,

            MediumSeverity = snapshot.MediumSeverity,

            LowSeverity = snapshot.LowSeverity,

            TotalUnitsDiscrepancy =
                snapshot.TotalUnitsDiscrepancy,

            Results = snapshot.Results
                .Select(x => new AuditResultDto
                {
                    SKU = x.SKU,

                    ShopifyQuantity = x.ShopifyQuantity,

                    WarehouseQuantity = x.WarehouseQuantity,

                    Difference = x.Difference,

                    Status = x.Status,

                    Severity = x.Severity
                })
                .ToList()
        };
    }
}
