using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class PartnerTrackingQueryPlanTests : IClassFixture<LocalDbIntegrationFixture>
{
    private readonly LocalDbIntegrationFixture _fixture;

    public PartnerTrackingQueryPlanTests(LocalDbIntegrationFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task TrackingLookup_UsesUniqueTrackingCodeIndexAndReturnsOnlyRequestedShop()
    {
        await _fixture.ExecuteAsync(async services =>
        {
            var dbContext = services.GetRequiredService<MiniLogisticsDbContext>();
            var shop = await dbContext.Shops.AsNoTracking().FirstAsync();
            var expected = Shipment.Create(
                shop.Id,
                "Query Plan Sender",
                new PhoneNumber("0900000010"),
                "Query Plan Receiver",
                new PhoneNumber("0900000011"),
                new Address("1 Nguyen Hue", "Ben Nghe", "Ho Chi Minh"),
                new Address("2 Le Loi", "Ben Thanh", "Ho Chi Minh"),
                new Weight(1m),
                new ParcelDimensions(10m, 10m, 10m),
                new Weight(1m),
                new Money(100_000m),
                Money.Zero,
                new ShippingFeeBreakdown(new Money(25_000m), Money.Zero, Money.Zero, Money.Zero),
                RouteType.IntraRegion,
                shop.OwnerUserId,
                DateTimeOffset.UtcNow,
                trackingCode: new TrackingCode("MLPLAN202608020001"));
            dbContext.Shipments.Add(expected);
            await dbContext.SaveChangesAsync();
            var repository = services.GetRequiredService<IShipmentReadRepository>();

            var shipment = await repository.GetByTrackingCodeAndShopIdAsync(
                expected.TrackingCode,
                expected.ShopId);
            var wrongShop = await repository.GetByTrackingCodeAndShopIdAsync(
                expected.TrackingCode,
                Guid.NewGuid());

            Assert.NotNull(shipment);
            Assert.Null(wrongShop);
            var plan = await GetTrackingPlanAsync(
                dbContext,
                expected.TrackingCode.Value,
                expected.ShopId);
            Assert.Contains("Index Seek", plan, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("IX_Shipments_TrackingCode", plan, StringComparison.Ordinal);
        });
    }

    private static async Task<string> GetTrackingPlanAsync(
        MiniLogisticsDbContext dbContext,
        string trackingCode,
        Guid shopId)
    {
        var connection = dbContext.Database.GetDbConnection();
        await connection.OpenAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SET STATISTICS XML ON; SELECT [Id] FROM [Shipments] WHERE [TrackingCode] = @TrackingCode AND [ShopId] = @ShopId; SET STATISTICS XML OFF;";
            AddParameter(command, "@TrackingCode", trackingCode, DbType.String);
            AddParameter(command, "@ShopId", shopId, DbType.Guid);
            await using var reader = await command.ExecuteReaderAsync();
            do
            {
                while (await reader.ReadAsync())
                {
                    for (var index = 0; index < reader.FieldCount; index++)
                    {
                        var value = reader.GetValue(index)?.ToString();
                        if (value?.Contains("ShowPlanXML", StringComparison.Ordinal) == true)
                        {
                            return value;
                        }
                    }
                }
            }
            while (await reader.NextResultAsync());

            return string.Empty;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }

    private static void AddParameter(
        System.Data.Common.DbCommand command,
        string name,
        object value,
        DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        parameter.DbType = dbType;
        command.Parameters.Add(parameter);
    }
}
