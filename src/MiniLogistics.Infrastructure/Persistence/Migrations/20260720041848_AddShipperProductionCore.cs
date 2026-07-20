using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddShipperProductionCore : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FailureReasonCode",
                table: "ShipmentStatusHistories",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GpsAccuracyMeters",
                table: "ShipmentStatusHistories",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GpsCapturedAtUtc",
                table: "ShipmentStatusHistories",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Latitude",
                table: "ShipmentStatusHistories",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Longitude",
                table: "ShipmentStatusHistories",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CollectedAmount",
                table: "CodTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectionNote",
                table: "CodTransactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "DiscrepancyAmount",
                table: "CodTransactions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DeliveryProofs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProofType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ProofMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ResourceUri = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RecipientName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    VerificationText = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Latitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(9,6)", precision: 9, scale: 6, nullable: true),
                    GpsAccuracyMeters = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    GpsCapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    SubmittedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CapturedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SubmittedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliveryProofs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeliveryProofs_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryProofs_ShipmentId_ProofType_SubmittedAtUtc",
                table: "DeliveryProofs",
                columns: new[] { "ShipmentId", "ProofType", "SubmittedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DeliveryProofs_SubmittedByUserId",
                table: "DeliveryProofs",
                column: "SubmittedByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeliveryProofs");

            migrationBuilder.DropColumn(
                name: "FailureReasonCode",
                table: "ShipmentStatusHistories");

            migrationBuilder.DropColumn(
                name: "GpsAccuracyMeters",
                table: "ShipmentStatusHistories");

            migrationBuilder.DropColumn(
                name: "GpsCapturedAtUtc",
                table: "ShipmentStatusHistories");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "ShipmentStatusHistories");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "ShipmentStatusHistories");

            migrationBuilder.DropColumn(
                name: "CollectedAmount",
                table: "CodTransactions");

            migrationBuilder.DropColumn(
                name: "CollectionNote",
                table: "CodTransactions");

            migrationBuilder.DropColumn(
                name: "DiscrepancyAmount",
                table: "CodTransactions");
        }
    }
}
