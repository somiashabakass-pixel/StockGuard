using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Infrastructure.Data.Configurations;

public class StoreConfiguration
    : IEntityTypeConfiguration<Store>
{
    public void Configure(
        EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("stores");

        builder.HasKey(x => x.Id);

        builder.HasAlternateKey(x => new
        {
            x.OrganizationId,
            x.Id
        });

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ShopifyDomain)
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(x => new
        {
            x.OrganizationId,
            x.ShopifyDomain
        }).IsUnique();

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
