using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StockGuard.Infrastructure.Data.Entities;

namespace StockGuard.Infrastructure.Data.Configurations;

public sealed class OrganizationMembershipConfiguration
    : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(
        EntityTypeBuilder<OrganizationMembership> builder)
    {
        builder.ToTable(
            "organization_memberships",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_organization_memberships_role",
                    "\"Role\" IN (1, 2, 3, 4)");
            });

        builder.HasKey(x => new
        {
            x.UserId,
            x.OrganizationId
        });

        builder.Property(x => x.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Organization)
            .WithMany()
            .HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new
        {
            x.OrganizationId,
            x.IsActive
        })
        .HasDatabaseName(
            "IX_organization_memberships_organization_active");

        builder.HasIndex(x => new
        {
            x.UserId,
            x.IsActive
        })
        .HasDatabaseName(
            "IX_organization_memberships_user_active");
    }
}
