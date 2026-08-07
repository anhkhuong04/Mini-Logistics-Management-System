using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerApiProductionReadiness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveries_Status_NextAttemptAtUtc",
                table: "WebhookDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAtUtc",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<Guid>(
                name: "AttemptId",
                table: "WebhookDeliveries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                table: "WebhookDeliveries",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedUntilUtc",
                table: "WebhookDeliveries",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "WebhookDeliveries",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<Guid>(
                name: "AttemptId",
                table: "OutboxMessages",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LockedBy",
                table: "OutboxMessages",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedUntilUtc",
                table: "OutboxMessages",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "OutboxMessages",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_Status_NextAttemptAtUtc_LockedUntilUtc",
                table: "WebhookDeliveries",
                columns: new[] { "Status", "NextAttemptAtUtc", "LockedUntilUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAtUtc_LockedUntilUtc",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAtUtc", "LockedUntilUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveries_Status_NextAttemptAtUtc_LockedUntilUtc",
                table: "WebhookDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAtUtc_LockedUntilUtc",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "AttemptId",
                table: "WebhookDeliveries");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                table: "WebhookDeliveries");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                table: "WebhookDeliveries");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WebhookDeliveries");

            migrationBuilder.DropColumn(
                name: "AttemptId",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LockedBy",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LockedUntilUtc",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "OutboxMessages");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_Status_NextAttemptAtUtc",
                table: "WebhookDeliveries",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status_NextAttemptAtUtc",
                table: "OutboxMessages",
                columns: new[] { "Status", "NextAttemptAtUtc" });
        }
    }
}
