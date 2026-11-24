using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockGuard.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "organizations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "stores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ShopifyDomain = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stores", x => x.Id);
                    table.UniqueConstraint("AK_stores_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_stores_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "warehouses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_warehouses", x => x.Id);
                    table.UniqueConstraint("AK_warehouses_OrganizationId_Id", x => new { x.OrganizationId, x.Id });
                    table.ForeignKey(
                        name: "FK_warehouses_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrganizationId = table.Column<Guid>(type: "uuid", nullable: false),
                    StoreId = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TotalProducts = table.Column<int>(type: "integer", nullable: false),
                    MatchingProducts = table.Column<int>(type: "integer", nullable: false),
                    MismatchedProducts = table.Column<int>(type: "integer", nullable: false),
                    MissingProducts = table.Column<int>(type: "integer", nullable: false),
                    TotalUnitsDiscrepancy = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_runs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_runs_organizations_OrganizationId",
                        column: x => x.OrganizationId,
                        principalTable: "organizations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audit_runs_stores_OrganizationId_StoreId",
                        columns: x => new { x.OrganizationId, x.StoreId },
                        principalTable: "stores",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_audit_runs_warehouses_OrganizationId_WarehouseId",
                        columns: x => new { x.OrganizationId, x.WarehouseId },
                        principalTable: "warehouses",
                        principalColumns: new[] { "OrganizationId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AuditRunId = table.Column<Guid>(type: "uuid", nullable: false),
                    SKU = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ShopifyQuantity = table.Column<int>(type: "integer", nullable: true),
                    WarehouseQuantity = table.Column<int>(type: "integer", nullable: true),
                    Difference = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_results", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_results_audit_runs_AuditRunId",
                        column: x => x.AuditRunId,
                        principalTable: "audit_runs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_results_AuditRunId_Severity",
                table: "audit_results",
                columns: new[] { "AuditRunId", "Severity" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_results_AuditRunId_SKU",
                table: "audit_results",
                columns: new[] { "AuditRunId", "SKU" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_audit_runs_OrganizationId_StartedAtUtc",
                table: "audit_runs",
                columns: new[] { "OrganizationId", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_runs_OrganizationId_StoreId",
                table: "audit_runs",
                columns: new[] { "OrganizationId", "StoreId" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_runs_OrganizationId_WarehouseId",
                table: "audit_runs",
                columns: new[] { "OrganizationId", "WarehouseId" });

            migrationBuilder.CreateIndex(
                name: "IX_stores_OrganizationId_ShopifyDomain",
                table: "stores",
                columns: new[] { "OrganizationId", "ShopifyDomain" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_warehouses_OrganizationId_Name",
                table: "warehouses",
                columns: new[] { "OrganizationId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_results");

            migrationBuilder.DropTable(
                name: "audit_runs");

            migrationBuilder.DropTable(
                name: "stores");

            migrationBuilder.DropTable(
                name: "warehouses");

            migrationBuilder.DropTable(
                name: "organizations");
        }
    }
}
