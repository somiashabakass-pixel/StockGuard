namespace StockGuard.Infrastructure.Data.Entities;

public sealed class OrganizationMembership
{
    public Guid UserId { get; set; }

    public Guid OrganizationId { get; set; }

    public OrganizationRole Role { get; set; }
        = OrganizationRole.Viewer;

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public bool IsActive { get; set; } = false;

    public ApplicationUser User { get; set; } = null!;

    public Organization Organization { get; set; } = null!;
}
