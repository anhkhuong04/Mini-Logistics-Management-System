using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniLogistics.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddWebhookSecretVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SecretVersion",
                table: "WebhookEndpoints",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "ProtectedSigningSecret",
                table: "WebhookDeliveries",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SecretVersion",
                table: "WebhookDeliveries",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                EXEC(N'
                    UPDATE deliveries
                    SET deliveries.[ProtectedSigningSecret] = endpoints.[SigningSecret],
                        deliveries.[SecretVersion] = endpoints.[SecretVersion]
                    FROM [WebhookDeliveries] AS deliveries
                    INNER JOIN [WebhookEndpoints] AS endpoints
                        ON endpoints.[Id] = deliveries.[WebhookEndpointId]
                    WHERE deliveries.[ProtectedSigningSecret] IS NULL;

                    UPDATE messages
                    SET messages.[PayloadJson] = JSON_MODIFY(
                        JSON_MODIFY(
                            messages.[PayloadJson],
                            ''$.protectedSigningSecret'',
                            endpoints.[SigningSecret]),
                        ''$.secretVersion'',
                        endpoints.[SecretVersion])
                    FROM [OutboxMessages] AS messages
                    INNER JOIN [WebhookEndpoints] AS endpoints
                        ON endpoints.[Id] = TRY_CONVERT(
                            uniqueidentifier,
                            JSON_VALUE(
                                CASE WHEN ISJSON(messages.[PayloadJson]) = 1
                                    THEN messages.[PayloadJson]
                                    ELSE N''{}''
                                END,
                                ''$.webhookEndpointId''))
                    WHERE messages.[Type] IN (
                            N''WebhookShipmentCreated'',
                            N''WebhookShipmentStatusChanged'')
                      AND ISJSON(messages.[PayloadJson]) = 1
                      AND JSON_VALUE(
                            CASE WHEN ISJSON(messages.[PayloadJson]) = 1
                                THEN messages.[PayloadJson]
                                ELSE N''{}''
                            END,
                            ''$.protectedSigningSecret'') IS NULL;
                ');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SecretVersion",
                table: "WebhookEndpoints");

            migrationBuilder.DropColumn(
                name: "ProtectedSigningSecret",
                table: "WebhookDeliveries");

            migrationBuilder.DropColumn(
                name: "SecretVersion",
                table: "WebhookDeliveries");
        }
    }
}
