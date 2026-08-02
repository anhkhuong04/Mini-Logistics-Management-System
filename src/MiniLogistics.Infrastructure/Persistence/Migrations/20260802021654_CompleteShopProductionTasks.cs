using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CompleteShopProductionTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedIpAddresses",
                table: "ApiClients",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ExpiresAtUtc",
                table: "ApiClients",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Scopes",
                table: "ApiClients",
                type: "int",
                nullable: false,
                defaultValue: 31);

            migrationBuilder.CreateTable(
                name: "ShipmentImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    TotalRows = table.Column<int>(type: "int", nullable: false),
                    ValidRows = table.Column<int>(type: "int", nullable: false),
                    CreatedRows = table.Column<int>(type: "int", nullable: false),
                    FailedRows = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentImportBatches_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShipmentImportBatches_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShopNotificationPreferences",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnabledEventTypes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopNotificationPreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopNotificationPreferences_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopNotificationPreferences_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShopNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShopNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShopNotifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopNotifications_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShopNotifications_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentImportBatchRows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ShopId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RowNumber = table.Column<int>(type: "int", nullable: false),
                    ClientOrderCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PayloadJson = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    IsValidAtSubmission = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ShipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TrackingCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Errors = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentImportBatchRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentImportBatchRows_ShipmentImportBatches_BatchId",
                        column: x => x.BatchId,
                        principalTable: "ShipmentImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ShipmentImportBatchRows_Shipments_ShipmentId",
                        column: x => x.ShipmentId,
                        principalTable: "Shipments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ShipmentImportBatchRows_Shops_ShopId",
                        column: x => x.ShopId,
                        principalTable: "Shops",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentImportBatches_RequestedByUserId",
                table: "ShipmentImportBatches",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentImportBatches_ShopId_CreatedAtUtc",
                table: "ShipmentImportBatches",
                columns: new[] { "ShopId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentImportBatches_Status_CreatedAtUtc",
                table: "ShipmentImportBatches",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentImportBatchRows_BatchId_RowNumber",
                table: "ShipmentImportBatchRows",
                columns: new[] { "BatchId", "RowNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentImportBatchRows_ShipmentId",
                table: "ShipmentImportBatchRows",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentImportBatchRows_ShopId_ClientOrderCode_Status",
                table: "ShipmentImportBatchRows",
                columns: new[] { "ShopId", "ClientOrderCode", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopNotificationPreferences_ShopId_UserId",
                table: "ShopNotificationPreferences",
                columns: new[] { "ShopId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ShopNotificationPreferences_UserId",
                table: "ShopNotificationPreferences",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopNotifications_ShipmentId",
                table: "ShopNotifications",
                column: "ShipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ShopNotifications_ShopId_UserId_IsRead_CreatedAtUtc",
                table: "ShopNotifications",
                columns: new[] { "ShopId", "UserId", "IsRead", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ShopNotifications_UserId",
                table: "ShopNotifications",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShipmentImportBatchRows");

            migrationBuilder.DropTable(
                name: "ShopNotificationPreferences");

            migrationBuilder.DropTable(
                name: "ShopNotifications");

            migrationBuilder.DropTable(
                name: "ShipmentImportBatches");

            migrationBuilder.DropColumn(
                name: "AllowedIpAddresses",
                table: "ApiClients");

            migrationBuilder.DropColumn(
                name: "ExpiresAtUtc",
                table: "ApiClients");

            migrationBuilder.DropColumn(
                name: "Scopes",
                table: "ApiClients");
        }
    }
}
