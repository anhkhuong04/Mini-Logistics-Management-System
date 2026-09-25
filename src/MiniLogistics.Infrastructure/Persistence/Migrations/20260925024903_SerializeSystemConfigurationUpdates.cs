using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SerializeSystemConfigurationUpdates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RouteRegionConfigs_Province_IsActive",
                table: "RouteRegionConfigs");

            migrationBuilder.DropIndex(
                name: "IX_RouteRegionConfigs_Province_Version",
                table: "RouteRegionConfigs");

            migrationBuilder.DropIndex(
                name: "IX_FeeRules_RouteType_IsActive",
                table: "FeeRules");

            migrationBuilder.CreateIndex(
                name: "IX_RouteRegionConfigs_Province_Version",
                table: "RouteRegionConfigs",
                columns: new[] { "Province", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_RouteRegionConfigs_Province_Active",
                table: "RouteRegionConfigs",
                column: "Province",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_FeeRules_RouteType_Version",
                table: "FeeRules",
                columns: new[] { "RouteType", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_FeeRules_RouteType_Active",
                table: "FeeRules",
                column: "RouteType",
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RouteRegionConfigs_Province_Version",
                table: "RouteRegionConfigs");

            migrationBuilder.DropIndex(
                name: "UX_RouteRegionConfigs_Province_Active",
                table: "RouteRegionConfigs");

            migrationBuilder.DropIndex(
                name: "IX_FeeRules_RouteType_Version",
                table: "FeeRules");

            migrationBuilder.DropIndex(
                name: "UX_FeeRules_RouteType_Active",
                table: "FeeRules");

            migrationBuilder.CreateIndex(
                name: "IX_RouteRegionConfigs_Province_IsActive",
                table: "RouteRegionConfigs",
                columns: new[] { "Province", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_RouteRegionConfigs_Province_Version",
                table: "RouteRegionConfigs",
                columns: new[] { "Province", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_FeeRules_RouteType_IsActive",
                table: "FeeRules",
                columns: new[] { "RouteType", "IsActive" });
        }
    }
}
