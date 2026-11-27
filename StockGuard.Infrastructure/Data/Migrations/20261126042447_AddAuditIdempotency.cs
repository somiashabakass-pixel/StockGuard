using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockGuard.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "audit_runs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "audit_runs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UX_audit_runs_organization_idempotency",
                table: "audit_runs",
                columns: new[] { "OrganizationId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_audit_runs_organization_idempotency",
                table: "audit_runs");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "audit_runs");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "audit_runs");
        }
    }
}
