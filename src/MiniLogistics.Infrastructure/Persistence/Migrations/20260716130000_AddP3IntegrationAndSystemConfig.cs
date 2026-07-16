using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    [Migration("20260716130000_AddP3IntegrationAndSystemConfig")]
    public partial class AddP3IntegrationAndSystemConfig : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "FeeRules",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceFreeThreshold",
                table: "FeeRules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 1000000m);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceMaximumValue",
                table: "FeeRules",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 20000000m);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceRate",
                table: "FeeRules",
                type: "decimal(10,6)",
                nullable: false,
                defaultValue: 0.005m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnFeeRate",
                table: "FeeRules",
                type: "decimal(10,4)",
                nullable: false,
                defaultValue: 0.5m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnFeeRate",
                table: "Shipments",
                type: "decimal(10,4)",
                nullable: false,
                defaultValue: 0.5m);

            migrationBuilder.AddColumn<long>(
                name: "LastDurationMs",
                table: "WebhookDeliveries",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RouteRegionConfigs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Region = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RouteRegionConfigs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntegrationManagementScopes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsGlobal = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntegrationManagementScopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntegrationManagementScopes_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RouteRegionConfigs_Province_IsActive",
                table: "RouteRegionConfigs",
                columns: new[] { "Province", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RouteRegionConfigs_Province_Version",
                table: "RouteRegionConfigs",
                columns: new[] { "Province", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationManagementScopes_ActorUserId_IsActive",
                table: "IntegrationManagementScopes",
                columns: new[] { "ActorUserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationManagementScopes_Province_IsActive",
                table: "IntegrationManagementScopes",
                columns: new[] { "Province", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_IntegrationManagementScopes_ShopId",
                table: "IntegrationManagementScopes",
                column: "ShopId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "IntegrationManagementScopes");
            migrationBuilder.DropTable(name: "RouteRegionConfigs");

            migrationBuilder.DropColumn(name: "LastDurationMs", table: "WebhookDeliveries");
            migrationBuilder.DropColumn(name: "ReturnFeeRate", table: "Shipments");
            migrationBuilder.DropColumn(name: "ReturnFeeRate", table: "FeeRules");
            migrationBuilder.DropColumn(name: "InsuranceRate", table: "FeeRules");
            migrationBuilder.DropColumn(name: "InsuranceMaximumValue", table: "FeeRules");
            migrationBuilder.DropColumn(name: "InsuranceFreeThreshold", table: "FeeRules");
            migrationBuilder.DropColumn(name: "Version", table: "FeeRules");
        }
    }
}
