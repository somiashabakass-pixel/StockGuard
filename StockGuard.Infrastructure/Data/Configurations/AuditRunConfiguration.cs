using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Infrastructure.Data.Configurations;

public class AuditRunConfiguration
    : IEntityTypeConfiguration<AuditRun>
{
    public void Configure(
        EntityTypeBuilder<AuditRun> builder)
    {
        builder.ToTable("audit_runs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(x => new
        {
            x.OrganizationId,
            x.StartedAtUtc
        });

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Store)
            .WithMany()
            .HasForeignKey(x => new
            {
                x.OrganizationId,
                x.StoreId
            })
            .HasPrincipalKey(x => new
            {
                x.OrganizationId,
                x.Id
            })
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => new
            {
                x.OrganizationId,
                x.WarehouseId
            })
            .HasPrincipalKey(x => new
            {
                x.OrganizationId,
                x.Id
            })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
