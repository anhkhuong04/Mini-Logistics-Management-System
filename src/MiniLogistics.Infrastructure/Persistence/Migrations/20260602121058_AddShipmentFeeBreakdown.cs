using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentFeeBreakdown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Shipments
                SET RouteType = 'InterRegion'
                WHERE RouteType = 'InterProvince'
                """);

            migrationBuilder.DeleteData(
                table: "FeeRules",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"));

            migrationBuilder.AddColumn<decimal>(
                name: "BaseShippingFee",
                table: "Shipments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtraWeightFee",
                table: "Shipments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "InsuranceFee",
                table: "Shipments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReturnFee",
                table: "Shipments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalShippingFee",
                table: "Shipments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE Shipments
                SET
                    BaseShippingFee = ShippingFee,
                    ExtraWeightFee = 0,
                    InsuranceFee = 0,
                    ReturnFee = 0,
                    TotalShippingFee = ShippingFee
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseShippingFee",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ExtraWeightFee",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "InsuranceFee",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ReturnFee",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "TotalShippingFee",
                table: "Shipments");

            migrationBuilder.InsertData(
                table: "FeeRules",
                columns: new[] { "Id", "BaseFee", "BaseWeightKg", "CreatedAtUtc", "ExtraStepFee", "ExtraWeightStepKg", "IsActive", "MaximumWeightKg", "MinimumWeightKg", "RouteType", "UpdatedAtUtc" },
                values: new object[] { new Guid("44444444-4444-4444-4444-444444444444"), 35000m, 0.5m, new DateTimeOffset(new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), 8000m, 0.5m, true, null, null, "InterProvince", null });
        }
    }
}
