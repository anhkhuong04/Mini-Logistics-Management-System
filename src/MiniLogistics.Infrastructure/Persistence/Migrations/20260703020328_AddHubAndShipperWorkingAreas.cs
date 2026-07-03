using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHubAndShipperWorkingAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hubs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ward = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AddressLine = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Country = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsRegionalSortingHub = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hubs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShipperWorkingAreas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShipperId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HubId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Province = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ward = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ZoneCode = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipperWorkingAreas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipperWorkingAreas_AspNetUsers_ShipperId",
                        column: x => x.ShipperId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShipperWorkingAreas_Hubs_HubId",
                        column: x => x.HubId,
                        principalTable: "Hubs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Hubs_Code",
                table: "Hubs",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hubs_IsActive",
                table: "Hubs",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Hubs_Province",
                table: "Hubs",
                column: "Province");

            migrationBuilder.CreateIndex(
                name: "IX_ShipperWorkingAreas_HubId",
                table: "ShipperWorkingAreas",
                column: "HubId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipperWorkingAreas_IsActive",
                table: "ShipperWorkingAreas",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ShipperWorkingAreas_Province",
                table: "ShipperWorkingAreas",
                column: "Province");

            migrationBuilder.CreateIndex(
                name: "IX_ShipperWorkingAreas_ShipperId",
                table: "ShipperWorkingAreas",
                column: "ShipperId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipperWorkingAreas_ShipperId_HubId_Ward_ZoneCode",
                table: "ShipperWorkingAreas",
                columns: new[] { "ShipperId", "HubId", "Ward", "ZoneCode" },
                unique: true,
                filter: "[IsActive] = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipperWorkingAreas");

            migrationBuilder.DropTable(
                name: "Hubs");
        }
    }
}
