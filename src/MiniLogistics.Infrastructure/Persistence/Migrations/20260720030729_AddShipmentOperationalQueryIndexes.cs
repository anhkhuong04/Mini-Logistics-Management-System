using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentOperationalQueryIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Shipments_PickupProvince",
                table: "Shipments",
                column: "PickupProvince");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_ShopId_Status_CreatedAtUtc",
                table: "Shipments",
                columns: new[] { "ShopId", "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_Status_CreatedAtUtc",
                table: "Shipments",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentAssignments_ShipperId_UnassignedAtUtc_AssignedAtUtc",
                table: "ShipmentAssignments",
                columns: new[] { "ShipperId", "UnassignedAtUtc", "AssignedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipments_PickupProvince",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_ShopId_Status_CreatedAtUtc",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_Status_CreatedAtUtc",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_ShipmentAssignments_ShipperId_UnassignedAtUtc_AssignedAtUtc",
                table: "ShipmentAssignments");
        }
    }
}
