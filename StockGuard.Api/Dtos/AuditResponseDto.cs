namespace StockGuard.Api.Dtos;

public class AuditResponseDto
{
    public Guid? AuditId { get; set; }

    public DateTime CompletedAtUtc { get; set; }

    public int TotalProducts { get; set; }

    public int MatchingProducts { get; set; }

    public int MismatchedProducts { get; set; }

    public int MissingProducts { get; set; }

    public int HighSeverity { get; set; }

    public int MediumSeverity { get; set; }

    public int LowSeverity { get; set; }

    public int TotalUnitsDiscrepancy { get; set; }

    public List<AuditResultDto> Results
        { get; set; } = new();
}
