using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Infrastructure.Data.Configurations;

public class AuditResultConfiguration
    : IEntityTypeConfiguration<AuditResultEntity>
{
    public void Configure(
        EntityTypeBuilder<AuditResultEntity> builder)
    {
        builder.ToTable("audit_results");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SKU)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Severity)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(x => new
        {
            x.AuditRunId,
            x.SKU
        }).IsUnique();

        builder.HasIndex(x => new
        {
            x.AuditRunId,
            x.Severity
        });

        builder.HasOne(x => x.AuditRun)
            .WithMany()
            .HasForeignKey(x => x.AuditRunId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
