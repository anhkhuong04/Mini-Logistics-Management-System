using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RecordShipmentAppliedConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AppliedFeeRuleId",
                table: "Shipments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AppliedFeeRuleVersion",
                table: "Shipments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeliveryRouteRegionConfigId",
                table: "Shipments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DeliveryRouteRegionConfigVersion",
                table: "Shipments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PickupRouteRegionConfigId",
                table: "Shipments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PickupRouteRegionConfigVersion",
                table: "Shipments",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_AppliedFeeRuleId",
                table: "Shipments",
                column: "AppliedFeeRuleId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_DeliveryRouteRegionConfigId",
                table: "Shipments",
                column: "DeliveryRouteRegionConfigId");

            migrationBuilder.CreateIndex(
                name: "IX_Shipments_PickupRouteRegionConfigId",
                table: "Shipments",
                column: "PickupRouteRegionConfigId");

            migrationBuilder.AddForeignKey(
                name: "FK_Shipments_FeeRules_AppliedFeeRuleId",
                table: "Shipments",
                column: "AppliedFeeRuleId",
                principalTable: "FeeRules",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Shipments_RouteRegionConfigs_DeliveryRouteRegionConfigId",
                table: "Shipments",
                column: "DeliveryRouteRegionConfigId",
                principalTable: "RouteRegionConfigs",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Shipments_RouteRegionConfigs_PickupRouteRegionConfigId",
                table: "Shipments",
                column: "PickupRouteRegionConfigId",
                principalTable: "RouteRegionConfigs",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shipments_FeeRules_AppliedFeeRuleId",
                table: "Shipments");

            migrationBuilder.DropForeignKey(
                name: "FK_Shipments_RouteRegionConfigs_DeliveryRouteRegionConfigId",
                table: "Shipments");

            migrationBuilder.DropForeignKey(
                name: "FK_Shipments_RouteRegionConfigs_PickupRouteRegionConfigId",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_AppliedFeeRuleId",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_DeliveryRouteRegionConfigId",
                table: "Shipments");

            migrationBuilder.DropIndex(
                name: "IX_Shipments_PickupRouteRegionConfigId",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "AppliedFeeRuleId",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "AppliedFeeRuleVersion",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "DeliveryRouteRegionConfigId",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "DeliveryRouteRegionConfigVersion",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "PickupRouteRegionConfigId",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "PickupRouteRegionConfigVersion",
                table: "Shipments");
        }
    }
}
