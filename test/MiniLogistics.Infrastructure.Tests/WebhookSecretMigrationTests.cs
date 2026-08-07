using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniLogistics.Application.Outbox;
using MiniLogistics.Application.PartnerApi;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class WebhookSecretMigrationTests
{
    private const string PreviousMigration = "20260802040549_AddPartnerApiProductionReadiness";

    [Fact]
    public async Task Upgrade_BackfillsSecretSnapshotForExistingDeliveryAndOutbox()
    {
        var databaseName = $"MiniLogisticsSecretMigration_{Guid.NewGuid():N}";
        var connectionString =
            $"Server=(localdb)\\MSSQLLocalDB;Database={databaseName};Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<MiniLogisticsDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await using var dbContext = new MiniLogisticsDbContext(options);

        try
        {
            var migrator = dbContext.GetService<IMigrator>();
            await migrator.MigrateAsync(PreviousMigration);

            var now = new DateTimeOffset(2026, 8, 2, 10, 0, 0, TimeSpan.Zero);
            var shop = new Shop(
                Guid.NewGuid(),
                "Migration test shop",
                new PhoneNumber("0900000001"),
                new Address("9 Le Loi", "Ben Nghe", "Ho Chi Minh"),
                now);
            var apiClient = new ApiClient(
                shop.Id,
                "Migration client",
                "ml_test_migration",
                new string('A', 64),
                now);
            dbContext.Shops.Add(shop);
            dbContext.ApiClients.Add(apiClient);
            await dbContext.SaveChangesAsync();

            var endpointId = Guid.NewGuid();
            var deliveryId = Guid.NewGuid();
            var outboxId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();
            const string protectedSecret = "protected-secret-v1";
            const string emptyPayload = "{}";
            var oldOutboxPayload = JsonSerializer.Serialize(new
            {
                webhookEndpointId = endpointId,
                apiClientId = apiClient.Id,
                eventType = WebhookEventTypes.ShipmentStatusChanged,
                aggregateId,
                webhookPayloadJson = "{\"event\":\"shipment.status_changed\"}"
            });

            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO [WebhookEndpoints]
                    ([Id], [ApiClientId], [Url], [SigningSecret], [IsActive], [CreatedAtUtc])
                VALUES
                    ({endpointId}, {apiClient.Id}, N'https://partner.example.test/webhook', {protectedSecret}, 1, {now});

                INSERT INTO [WebhookDeliveries]
                    ([Id], [WebhookEndpointId], [ApiClientId], [EventType], [AggregateId],
                     [PayloadJson], [Status], [RetryCount], [NextAttemptAtUtc], [CreatedAtUtc])
                VALUES
                    ({deliveryId}, {endpointId}, {apiClient.Id}, {WebhookEventTypes.ShipmentStatusChanged}, {aggregateId},
                     {emptyPayload}, N'Pending', 0, {now}, {now});

                INSERT INTO [OutboxMessages]
                    ([Id], [Type], [AggregateId], [PayloadJson], [Status], [RetryCount],
                     [NextAttemptAtUtc], [CreatedAtUtc])
                VALUES
                    ({outboxId}, {OutboxMessageTypes.WebhookShipmentStatusChanged}, {aggregateId}, {oldOutboxPayload},
                     N'Pending', 0, {now}, {now});
                """);

            await migrator.MigrateAsync();

            var deliverySnapshot = await ReadSnapshotAsync(
                dbContext,
                """
                SELECT [ProtectedSigningSecret], [SecretVersion]
                FROM [WebhookDeliveries]
                WHERE [Id] = @Id
                """,
                deliveryId);
            var outboxSnapshot = await ReadSnapshotAsync(
                dbContext,
                """
                SELECT
                    JSON_VALUE([PayloadJson], '$.protectedSigningSecret'),
                    TRY_CONVERT(int, JSON_VALUE([PayloadJson], '$.secretVersion'))
                FROM [OutboxMessages]
                WHERE [Id] = @Id
                """,
                outboxId);

            Assert.Equal((protectedSecret, 1), deliverySnapshot);
            Assert.Equal((protectedSecret, 1), outboxSnapshot);
        }
        finally
        {
            await dbContext.Database.EnsureDeletedAsync();
        }
    }

    private static async Task<(string Secret, int Version)> ReadSnapshotAsync(
        MiniLogisticsDbContext dbContext,
        string commandText,
        Guid id)
    {
        var connection = dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = commandText;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@Id";
            parameter.DbType = DbType.Guid;
            parameter.Value = id;
            command.Parameters.Add(parameter);
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            return (reader.GetString(0), reader.GetInt32(1));
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }
}
