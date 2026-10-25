using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.Operations;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class OperationsSettlementReadRepositoryTests(LocalDbIntegrationFixture fixture) : IClassFixture<LocalDbIntegrationFixture>
{
    [Fact]
    public async Task SettlementQuery_TranslatesJoinOrderingCountAndPagingInSql()
    {
        await using var scope = fixture.ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>(); var shop = await db.Shops.FirstAsync();
        var actor = await db.Users.Select(item => item.Id).FirstAsync();
        for (var index = 0; index < 4; index++)
        {
            var shipment = Shipment.Create(shop.Id, "Sender", new PhoneNumber("0901234567"), "Receiver", new PhoneNumber("0901234567"),
                new Address("Street", "Ward", "Province", "Vietnam"), new Address("Street", "Ward", "Province", "Vietnam"),
                new Weight(1), new ParcelDimensions(10, 10, 10), new Weight(1), Money.Zero, new Money(1000 + index),
                new ShippingFeeBreakdown(new Money(25000), Money.Zero, Money.Zero, Money.Zero), RouteType.IntraProvince, actor,
                DateTimeOffset.UtcNow, null, new TrackingCode($"ML-SQL-OPS-{index}"));
            var cod = CodTransaction.Create(shipment.Id, shipment.CodAmount, DateTimeOffset.UtcNow);
            if (index < 3) Assert.True(cod.MarkCollected(ShipmentStatus.Delivered, actor, DateTimeOffset.UtcNow.AddMinutes(index)).IsSuccess);
            db.Shipments.Add(shipment); db.CodTransactions.Add(cod);
        }
        await db.SaveChangesAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IOperationsSettlementReadRepository>();
        var page = await repository.SearchAsync(2, 1, default);
        Assert.Equal(3, page.TotalCount); Assert.Equal(3, page.TotalPages);
        var item = Assert.Single(page.Items); Assert.Equal("ML-SQL-OPS-1", item.TrackingCode); Assert.Equal(1001m, item.CodAmount);
        Assert.Equal(actor, item.CollectedByUserId);
        Assert.Empty((await repository.SearchAsync(4, 1, default)).Items);
    }
}
