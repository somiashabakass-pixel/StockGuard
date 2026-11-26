namespace StockGuard.Api.Dtos;

public class AuditHistoryDto
{
    public Guid AuditId { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string Status { get; set; } = string.Empty;

    public int TotalProducts { get; set; }

    public int MatchingProducts { get; set; }

    public int MismatchedProducts { get; set; }

    public int MissingProducts { get; set; }

    public int TotalUnitsDiscrepancy { get; set; }
}
