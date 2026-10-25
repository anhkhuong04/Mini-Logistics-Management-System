using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserShopProfileTests
{
    [Fact]
    public async Task Profile_PreservesMaskingPermissionsSnapshotsActorAndInactivePolicy()
    {
        await using var factory = new BrowserIdentityFactory(); using var owner = factory.Client(); using var staff = factory.Client();
        var ownerId = await factory.AddUser("profile-owner@example.test", "Shop"); var staffId = await factory.AddUser("profile-staff@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, ownerId, staffId);
        var shipment = BrowserShopShipmentReadTests.NewShipment(shop.Id, ownerId, "ML-PROFILE-SNAPSHOT");
        using (var scope = factory.Services.CreateScope()) { var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>(); db.Shipments.Add(shipment); await db.SaveChangesAsync(); }
        var path = $"/api/v1/app/shops/{shop.Id}/profile";
        Assert.Equal(HttpStatusCode.Unauthorized, (await owner.GetAsync(path)).StatusCode);
        await BrowserShopShipmentReadTests.Login(owner, "profile-owner@example.test"); await BrowserIdentityFactory.Csrf(owner);
        await BrowserShopShipmentReadTests.Login(staff, "profile-staff@example.test"); await BrowserIdentityFactory.Csrf(staff);
        var masked = await staff.GetFromJsonAsync<JsonElement>(path); Assert.NotEqual(shop.PhoneNumber.Value, masked.GetProperty("phoneNumber").GetString());
        var address = (await BrowserShippingQuoteTests.Request(owner)).PickupAddress;
        var body = new { name = "Renamed Shop", phoneNumber = "0909876543", addressLine = "New street", address.Ward, address.Province, country = "Vietnam", currentUserId = Guid.NewGuid(), shopId = Guid.NewGuid() };
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync(path, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PostAsJsonAsync("/api/v1/app/shops", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PutAsJsonAsync($"/api/v1/app/shops/{Guid.NewGuid()}/profile", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(path, new { body.name, phoneNumber = "invalid", body.addressLine, body.Ward, body.Province })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(path, new { body.name, body.phoneNumber, body.addressLine, ward = "invalid", body.Province })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync(path, body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PostAsJsonAsync("/api/v1/app/shops", new { body.name, body.phoneNumber, body.addressLine, ward = "invalid", body.Province })).StatusCode);
        var create = await owner.PostAsJsonAsync("/api/v1/app/shops", body); Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>(); var newId = created.GetProperty("shopId").GetGuid(); Assert.NotEqual(shop.Id, newId);
        using var verify = factory.Services.CreateScope(); var state = verify.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        Assert.Equal(ownerId, (await state.Shops.FindAsync(newId))!.OwnerUserId);
        Assert.Equal("Pickup private", (await state.Shipments.SingleAsync()).PickupAddress.Street);
        Assert.True(await state.AdminAuditLogs.AnyAsync(item => item.TargetId == shop.Id && item.ActorUserId == ownerId));
        (await state.Shops.FindAsync(shop.Id))!.Deactivate(DateTimeOffset.UtcNow); await state.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PutAsJsonAsync(path, body)).StatusCode);
    }
}
