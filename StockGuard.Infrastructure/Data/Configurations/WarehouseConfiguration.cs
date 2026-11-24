using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Infrastructure.Data.Configurations;

public class WarehouseConfiguration
    : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(
        EntityTypeBuilder<Warehouse> builder)
    {
        builder.ToTable("warehouses");

        builder.HasKey(x => x.Id);

        builder.HasAlternateKey(x => new
        {
            x.OrganizationId,
            x.Id
        });

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(x => new
        {
            x.OrganizationId,
            x.Name
        }).IsUnique();

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
