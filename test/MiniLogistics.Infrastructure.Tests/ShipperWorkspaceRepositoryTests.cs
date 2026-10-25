using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Application.Shipments;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Infrastructure.Tests;

public sealed class ShipperWorkspaceRepositoryTests(LocalDbIntegrationFixture fixture) : IClassFixture<LocalDbIntegrationFixture>
{
    [Fact]
    public async Task SelfSearch_NotRequiredIncludesExplicitZeroCodAndLegacyAbsentRows_AndKeepsSqlPaging()
    {
        await using var scope = fixture.ServiceProvider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>(); var shop = await db.Shops.FirstAsync();
        var actor = await db.Users.Select(item => item.Id).FirstAsync();
        for (var index = 0; index < 3; index++)
        {
            var shipment = Shipment.Create(shop.Id, "Sender", new PhoneNumber("0901234567"), "Receiver", new PhoneNumber("0901234567"),
                new Address("Street", "Ward", "Province", "Vietnam"), new Address("Street", "Ward", "Province", "Vietnam"),
                new Weight(1), new ParcelDimensions(10, 10, 10), new Weight(1), Money.Zero, new Money(index == 2 ? 1000 : 0),
                new ShippingFeeBreakdown(new Money(25000), Money.Zero, Money.Zero, Money.Zero), RouteType.IntraProvince,
                actor, DateTimeOffset.UtcNow.AddSeconds(index), null, new TrackingCode($"ML-SQL-SELF-{index}"));
            Assert.True(shipment.AssignShipper(actor, actor, DateTimeOffset.UtcNow, "seed").IsSuccess);
            db.Shipments.Add(shipment);
            if (index > 0) db.CodTransactions.Add(CodTransaction.Create(shipment.Id, shipment.CodAmount, DateTimeOffset.UtcNow));
        }
        await db.SaveChangesAsync();
        var repository = scope.ServiceProvider.GetRequiredService<IShipmentReadRepository>();
        var page = await repository.SearchAssignedToShipperAsync(new(actor, null, null, CodStatus.NotRequired, 1, 1));
        Assert.Equal(2, page.TotalCount); Assert.Equal("ML-SQL-SELF-1", Assert.Single(page.Items).TrackingCode.Value);
        Assert.Equal("ML-SQL-SELF-0", Assert.Single((await repository.SearchAssignedToShipperAsync(new(actor, null, null, CodStatus.NotRequired, 2, 1))).Items).TrackingCode.Value);
        Assert.Empty((await repository.SearchAssignedToShipperAsync(new(Guid.NewGuid(), null, null, null, 1, 20))).Items);
    }
}
