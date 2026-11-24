namespace StockGuard.Infrastructure.Data.Entities;

public class Warehouse
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OrganizationId { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    public Organization Organization { get; set; }
        = null!;
}
