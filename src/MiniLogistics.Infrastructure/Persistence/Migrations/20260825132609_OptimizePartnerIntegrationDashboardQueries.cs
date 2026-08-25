using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OptimizePartnerIntegrationDashboardQueries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveries_ApiClientId",
                table: "WebhookDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApiRequestAudits_ApiClientId_CreatedAtUtc",
                table: "PartnerApiRequestAudits");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApiCredentialAudits_ApiClientId_CreatedAtUtc",
                table: "PartnerApiCredentialAudits");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_ApiClientId_CreatedAtUtc_Id",
                table: "WebhookDeliveries",
                columns: new[] { "ApiClientId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApiRequestAudits_ApiClientId_CreatedAtUtc_Id",
                table: "PartnerApiRequestAudits",
                columns: new[] { "ApiClientId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApiCredentialAudits_ApiClientId_CreatedAtUtc_Id",
                table: "PartnerApiCredentialAudits",
                columns: new[] { "ApiClientId", "CreatedAtUtc", "Id" },
                descending: new[] { false, true, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WebhookDeliveries_ApiClientId_CreatedAtUtc_Id",
                table: "WebhookDeliveries");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApiRequestAudits_ApiClientId_CreatedAtUtc_Id",
                table: "PartnerApiRequestAudits");

            migrationBuilder.DropIndex(
                name: "IX_PartnerApiCredentialAudits_ApiClientId_CreatedAtUtc_Id",
                table: "PartnerApiCredentialAudits");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_ApiClientId",
                table: "WebhookDeliveries",
                column: "ApiClientId");

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApiRequestAudits_ApiClientId_CreatedAtUtc",
                table: "PartnerApiRequestAudits",
                columns: new[] { "ApiClientId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PartnerApiCredentialAudits_ApiClientId_CreatedAtUtc",
                table: "PartnerApiCredentialAudits",
                columns: new[] { "ApiClientId", "CreatedAtUtc" });
        }
    }
}
