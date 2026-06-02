using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GhnLikeCoreShippingFee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PerKilogramFee",
                table: "FeeRules",
                newName: "ExtraStepFee");

            migrationBuilder.AddColumn<decimal>(
                name: "ChargeableWeightInKg",
                table: "Shipments",
                type: "decimal(10,3)",
                precision: 10,
                scale: 3,
                nullable: false,
                defaultValue: 1m);

            migrationBuilder.AddColumn<decimal>(
                name: "ParcelHeightCm",
                table: "Shipments",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AddColumn<decimal>(
                name: "ParcelLengthCm",
                table: "Shipments",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 20m);

            migrationBuilder.AddColumn<decimal>(
                name: "ParcelWidthCm",
                table: "Shipments",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: false,
                defaultValue: 10m);

            migrationBuilder.AddColumn<decimal>(
                name: "BaseWeightKg",
                table: "FeeRules",
                type: "decimal(10,3)",
                precision: 10,
                scale: 3,
                nullable: false,
                defaultValue: 0.5m);

            migrationBuilder.AddColumn<decimal>(
                name: "ExtraWeightStepKg",
                table: "FeeRules",
                type: "decimal(10,3)",
                precision: 10,
                scale: 3,
                nullable: false,
                defaultValue: 0.5m);

            migrationBuilder.Sql("""
                UPDATE Shipments
                SET
                    ParcelLengthCm = CASE WHEN ParcelLengthCm <= 0 THEN 20 ELSE ParcelLengthCm END,
                    ParcelWidthCm = CASE WHEN ParcelWidthCm <= 0 THEN 10 ELSE ParcelWidthCm END,
                    ParcelHeightCm = CASE WHEN ParcelHeightCm <= 0 THEN 10 ELSE ParcelHeightCm END,
                    ChargeableWeightInKg =
                        CASE
                            WHEN WeightInKg >= CAST(20 * 10 * 10 AS decimal(10, 3)) / 5000 THEN WeightInKg
                            ELSE CAST(20 * 10 * 10 AS decimal(10, 3)) / 5000
                        END
                """);

            migrationBuilder.UpdateData(
                table: "FeeRules",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                columns: new[] { "BaseWeightKg", "ExtraStepFee", "ExtraWeightStepKg" },
                values: new object[] { 2.0m, 3000m, 0.5m });

            migrationBuilder.UpdateData(
                table: "FeeRules",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "BaseFee", "BaseWeightKg", "ExtraStepFee", "ExtraWeightStepKg" },
                values: new object[] { 28000m, 0.5m, 4000m, 0.5m });

            migrationBuilder.UpdateData(
                table: "FeeRules",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                columns: new[] { "BaseFee", "BaseWeightKg", "ExtraWeightStepKg" },
                values: new object[] { 35000m, 0.5m, 0.5m });

            migrationBuilder.UpdateData(
                table: "FeeRules",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                columns: new[] { "BaseFee", "BaseWeightKg", "ExtraStepFee", "ExtraWeightStepKg" },
                values: new object[] { 35000m, 0.5m, 8000m, 0.5m });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChargeableWeightInKg",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ParcelHeightCm",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ParcelLengthCm",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "ParcelWidthCm",
                table: "Shipments");

            migrationBuilder.DropColumn(
                name: "BaseWeightKg",
                table: "FeeRules");

            migrationBuilder.DropColumn(
                name: "ExtraWeightStepKg",
                table: "FeeRules");

            migrationBuilder.RenameColumn(
                name: "ExtraStepFee",
                table: "FeeRules",
                newName: "PerKilogramFee");

            migrationBuilder.UpdateData(
                table: "FeeRules",
                keyColumn: "Id",
                keyValue: new Guid("11111111-1111-1111-1111-111111111111"),
                column: "PerKilogramFee",
                value: 5000m);

            migrationBuilder.UpdateData(
                table: "FeeRules",
                keyColumn: "Id",
                keyValue: new Guid("22222222-2222-2222-2222-222222222222"),
                columns: new[] { "BaseFee", "PerKilogramFee" },
                values: new object[] { 30000m, 6000m });

            migrationBuilder.UpdateData(
                table: "FeeRules",
                keyColumn: "Id",
                keyValue: new Guid("33333333-3333-3333-3333-333333333333"),
                column: "BaseFee",
                value: 45000m);

            migrationBuilder.UpdateData(
                table: "FeeRules",
                keyColumn: "Id",
                keyValue: new Guid("44444444-4444-4444-4444-444444444444"),
                columns: new[] { "BaseFee", "PerKilogramFee" },
                values: new object[] { 60000m, 10000m });
        }
    }
}
