using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.AdminAuditing;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserShopSettingsTests
{
    [Theory]
    [InlineData("Admin")]
    [InlineData("Operator")]
    [InlineData("Shipper")]
    [InlineData("IntegrationAdmin")]
    public async Task OtherGlobalRoles_DoNotAcquireShopSettingsAccess(string role)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        await factory.AddUser("settings-role@example.test", role); var ownerId = await factory.AddUser("settings-real-owner@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, ownerId);
        await BrowserShopShipmentReadTests.Login(client, "settings-role@example.test");
        foreach (var part in new[] { "profile", "staff", "cod-report", "audit", "notifications", "notifications/preferences" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/app/shops/{shop.Id}/{part}")).StatusCode);
    }
    [Theory]
    [InlineData(ShopPermission.ViewCod, "cod-report", "audit")]
    [InlineData(ShopPermission.ViewAudit, "audit", "cod-report")]
    [InlineData(ShopPermission.ManageNotifications, "notifications/preferences", "cod-report")]
    public async Task CustomStaffRights_AreIndependent(ShopPermission granted, string allowed, string denied)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var ownerId = await factory.AddUser("custom-owner@example.test", "Shop"); var staffId = await factory.AddUser("custom-staff@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, ownerId, staffId);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            (await db.ShopStaffMemberships.SingleAsync()).SetAccess(ShopStaffRole.Custom, ShopPermission.ViewShipments | granted, DateTimeOffset.UtcNow); await db.SaveChangesAsync();
        }
        await BrowserShopShipmentReadTests.Login(client, "custom-staff@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/app/shops/{shop.Id}/{allowed}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/app/shops/{shop.Id}/{denied}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/app/shops/{shop.Id}/staff")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/shop/files/cod-report.csv?shopId={shop.Id}")).StatusCode);
    }
    [Fact]
    public async Task Staff_UsesUserIdStrictPresetsOwnerScopeAndImmediateRevocation()
    {
        await using var factory = new BrowserIdentityFactory(); using var owner = factory.Client(); using var staff = factory.Client();
        var ownerId = await factory.AddUser("settings-owner@example.test", "Shop"); var staffId = await factory.AddUser("settings-staff@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, ownerId, staffId); var prefix = $"/api/v1/app/shops/{shop.Id}";
        await BrowserShopShipmentReadTests.Login(owner, "settings-owner@example.test"); await BrowserIdentityFactory.Csrf(owner);
        await BrowserShopShipmentReadTests.Login(staff, "settings-staff@example.test"); await BrowserIdentityFactory.Csrf(staff);
        var access = new { role = "Viewer", permissions = new[] { "ViewShipments" }, isActive = false };
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(prefix + "/staff")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.PutAsJsonAsync(prefix + $"/staff/{staffId}", access)).StatusCode);
        var rows = await owner.GetFromJsonAsync<JsonElement>(prefix + "/staff"); var membershipId = rows[0].GetProperty("membershipId").GetGuid();
        Assert.Equal(staffId, rows[0].GetProperty("userId").GetGuid());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PutAsJsonAsync(prefix + $"/staff/{membershipId}", access)).StatusCode);
        foreach (var invalid in new object[] { new { role = "Manager", permissions = new[] { "ViewShipments" }, isActive = true }, new { role = "Custom", permissions = new[] { "Unknown" }, isActive = true }, new { role = 1, permissions = new[] { "ViewShipments" }, isActive = true } })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(prefix + $"/staff/{staffId}", invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await owner.PutAsJsonAsync($"/api/v1/app/shops/{Guid.NewGuid()}/staff/{staffId}", access)).StatusCode);
        var created = await owner.PostAsJsonAsync(prefix + "/staff", new { fullName = "New Staff", email = "new-settings-staff@example.test", phoneNumber = "0901234567", password = BrowserIdentityFactory.Password, role = "Viewer", permissions = new[] { "ViewShipments" } });
        Assert.Equal(HttpStatusCode.OK, created.StatusCode); Assert.DoesNotContain(BrowserIdentityFactory.Password, await created.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync(prefix + $"/staff/{staffId}", access)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staff.GetAsync(prefix + "/profile")).StatusCode);
    }

    [Fact]
    public async Task ReportsNotificationsAndAudit_EnforceIndependentPermissionsAndMaskSecrets()
    {
        await using var factory = new BrowserIdentityFactory(); using var owner = factory.Client(); using var viewer = factory.Client();
        var ownerId = await factory.AddUser("report-owner@example.test", "Shop"); var viewerId = await factory.AddUser("report-viewer@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, ownerId, viewerId);
        var shipment = BrowserShopShipmentReadTests.NewShipment(shop.Id, ownerId, "ML-SETTINGS-REPORT");
        var ownNotification = Guid.NewGuid(); var foreignNotification = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>(); db.Shipments.Add(shipment);
            db.CodTransactions.Add(CodTransaction.Create(shipment.Id, shipment.CodAmount, DateTimeOffset.UtcNow));
            db.ShopNotifications.Add(new ShopNotification(ownNotification, shop.Id, ownerId, "cod.collected", "Owner notice", "Safe notice", DateTimeOffset.UtcNow, shipment.Id));
            db.ShopNotifications.Add(new ShopNotification(foreignNotification, shop.Id, viewerId, "cod.collected", "Viewer notice", "Safe notice", DateTimeOffset.UtcNow));
            db.AdminAuditLogs.Add(new AdminAuditLog(ownerId, "Shop", "ProfileUpdated", "Shop", shop.Id, DateTimeOffset.UtcNow, newValueJson: "{\"password\":\"secret-fixture\",\"phoneNumber\":\"0901234567\"}"));
            await db.SaveChangesAsync();
        }
        await BrowserShopShipmentReadTests.Login(owner, "report-owner@example.test"); await BrowserIdentityFactory.Csrf(owner);
        await BrowserShopShipmentReadTests.Login(viewer, "report-viewer@example.test"); await BrowserIdentityFactory.Csrf(viewer);
        var prefix = $"/api/v1/app/shops/{shop.Id}";
        foreach (var part in new[] { "cod-report", "audit", "notifications/preferences" }) Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(prefix + "/" + part)).StatusCode);
        var report = await owner.GetFromJsonAsync<JsonElement>(prefix + "/cod-report"); Assert.Equal("9007199254740993.01", report.GetProperty("pendingCollectionAmount").GetString());
        Assert.Single(report.GetProperty("rows").EnumerateArray());
        var audit = await owner.GetAsync(prefix + "/audit?limit=1&action=ProfileUpdated"); Assert.Equal(HttpStatusCode.OK, audit.StatusCode); Assert.DoesNotContain("secret-fixture", await audit.Content.ReadAsStringAsync());
        foreach (var part in new[] { "cod-report", "audit", "notifications" }) Assert.Equal(HttpStatusCode.Forbidden, (await owner.GetAsync($"/api/v1/app/shops/{Guid.NewGuid()}/" + part)).StatusCode);
        var notifications = await owner.GetFromJsonAsync<JsonElement>(prefix + "/notifications"); Assert.Single(notifications.EnumerateArray()); Assert.Equal("Owner notice", notifications[0].GetProperty("title").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await owner.PostAsJsonAsync(prefix + $"/notifications/{foreignNotification}/read", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.PostAsJsonAsync(prefix + $"/notifications/{ownNotification}/read", new { })).StatusCode);
        Assert.True((await owner.GetFromJsonAsync<JsonElement>(prefix + "/notifications"))[0].GetProperty("isRead").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, (await owner.PutAsJsonAsync(prefix + "/notifications/preferences", new { enabledEventTypes = new[] { "unknown" } })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync(prefix + "/notifications/preferences", new { enabledEventTypes = new[] { "cod.collected" } })).StatusCode);
        Assert.Equal("cod.collected", (await owner.GetFromJsonAsync<JsonElement>(prefix + "/notifications/preferences")).GetProperty("enabledEventTypes")[0].GetString());
        foreach (var part in new[] { "cod-report?fromUtc=2026-10-25", "audit?limit=501", "notifications?limit=201", "cod-report?fromUtc=2026-10-26T00:00:00Z&toUtc=2026-10-25T00:00:00Z" })
            Assert.Equal(HttpStatusCode.BadRequest, (await owner.GetAsync(prefix + "/" + part)).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await viewer.GetAsync($"/shop/files/cod-report.csv?shopId={shop.Id}")).StatusCode);
    }
}
