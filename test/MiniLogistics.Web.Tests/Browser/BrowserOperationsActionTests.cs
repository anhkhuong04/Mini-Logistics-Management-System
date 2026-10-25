using System.Net;
using System.Net.Http.Json;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Infrastructure.Persistence;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserOperationsActionTests
{
    [Theory]
    [InlineData("Shop")]
    [InlineData("Shipper")]
    public async Task OperationsActions_DenySharedUseCaseRolesAtApplicationBoundary(string role)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var email = $"action-{role}@example.test"; var actor = await factory.AddUser(email, role);
        using (var scope = factory.Services.CreateScope())
        {
            var useCase = scope.ServiceProvider.GetRequiredService<MiniLogistics.Application.Operations.IOperationsActionService>();
            var denied = await useCase.StatusAsync(new MiniLogistics.Application.Shipments.UpdateShipmentStatus.UpdateShipmentStatusCommand(
                Guid.NewGuid(), actor, ShipmentStatus.PickingUp, null), default);
            Assert.True(denied.IsFailure); Assert.Equal("Application.Forbidden", denied.Error.Code);
        }
        await BrowserShopShipmentReadTests.Login(client, email); await BrowserIdentityFactory.Csrf(client);
        var path = $"/api/v1/app/operations/shipments/{Guid.NewGuid()}";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/status", new { newStatus = "PickingUp" })).StatusCode);
        foreach (var action in new[] { "collect", "settle" }) Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/cod/" + action, new { })).StatusCode);
    }
    [Fact]
    public async Task RealLifecycle_UsesPrincipalAudit_CollectsExactlyOnce_AndAdminSettles()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var actor = await factory.AddUser("lifecycle-operator@example.test", "Operator");
        await factory.AddUser("lifecycle-admin@example.test", "Admin");
        var owner = await factory.AddUser("lifecycle-shop@example.test", "Shop");
        var shipper = await factory.AddUser("lifecycle-shipper@example.test", "Shipper");
        var shop = await BrowserShippingQuoteTests.Seed(factory, owner);
        var shipment = BrowserShopShipmentReadTests.NewShipment(shop.Id, owner, "ML-OPS-LIFECYCLE");
        Assert.True(shipment.AssignShipper(shipper, actor, DateTimeOffset.UtcNow, "seed").IsSuccess);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            db.Shipments.Add(shipment); db.CodTransactions.Add(CodTransaction.Create(shipment.Id, shipment.CodAmount, DateTimeOffset.UtcNow)); await db.SaveChangesAsync();
        }
        await BrowserShopShipmentReadTests.Login(client, "lifecycle-operator@example.test"); await BrowserIdentityFactory.Csrf(client);
        var path = $"/api/v1/app/operations/shipments/{shipment.Id}";
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/cod/collect", new { })).StatusCode);
        foreach (var status in new[] { "PickingUp", "PickedUp", "InTransit", "Delivering", "Delivered" })
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(path + "/status", new { newStatus = status, note = "test", changedByUserId = owner })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/status", new { newStatus = "Delivered" })).StatusCode);
        var page = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/app/operations/shipments");
        var history = page.GetProperty("items")[0].GetProperty("trackingHistory");
        Assert.All(history.EnumerateArray(), item => { Assert.Equal(Guid.Empty, item.GetProperty("changedByUserId").GetGuid()); Assert.Equal(System.Text.Json.JsonValueKind.Null, item.GetProperty("changedByEmail").ValueKind); });
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(path + "/cod/collect", new { collectedAmount = "9007199254740993.01" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/cod/collect", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/cod/settle", new { })).StatusCode);
        using var admin = factory.Client(); await BrowserShopShipmentReadTests.Login(admin, "lifecycle-admin@example.test"); await BrowserIdentityFactory.Csrf(admin);
        var candidates = await admin.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/v1/app/operations/cod/settlement-candidates?pageSize=1");
        Assert.Equal("9007199254740993.01", candidates.GetProperty("items")[0].GetProperty("codAmount").GetString());
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync(path + "/cod/settle", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync(path + "/cod/settle", new { })).StatusCode);
        using var verify = factory.Services.CreateScope(); var state = verify.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        var saved = await state.Shipments.Include(item => item.StatusHistory).Include(item => item.Assignments).SingleAsync(item => item.Id == shipment.Id);
        Assert.All(saved.StatusHistory.Where(item => item.Status != ShipmentStatus.PendingPickup), item => Assert.Equal(actor, item.ChangedByUserId));
        Assert.DoesNotContain(saved.Assignments, item => item.IsActive);
        Assert.Equal(CodStatus.Settled, (await state.CodTransactions.SingleAsync()).Status);
    }
    [Fact]
    public async Task Operator_CannotSettle_AndStatusValidatesNoteEnumAndGps()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        await factory.AddUser("action-operator@example.test", "Operator"); await BrowserShopShipmentReadTests.Login(client, "action-operator@example.test"); await BrowserIdentityFactory.Csrf(client);
        var path = $"/api/v1/app/operations/shipments/{Guid.NewGuid()}";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/cod/settle", new { note = "settle" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/status", new { newStatus = "DeliveryFailed" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/status", new { newStatus = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path + "/status", new { newStatus = "PickingUp", gpsCoordinate = new { latitude = "91", longitude = "0" } })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync(path + "/cod/collect", new { })).StatusCode);
    }
}
