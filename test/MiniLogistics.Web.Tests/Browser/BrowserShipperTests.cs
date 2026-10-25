using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Infrastructure.Identity;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserShipperTests
{
    private const string Prefix = "/api/v1/app/shipper/me";
    [Fact]
    public async Task FormerAssignment_DoesNotGrantReadOrMutationAccess()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var (actor, _, own, _) = await Seed(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            var shipment = await db.Shipments.Include(item => item.Assignments).Include(item => item.StatusHistory).SingleAsync(item => item.Id == own.Id);
            Assert.True(shipment.CancelActiveAssignment(actor, DateTimeOffset.UtcNow, "Replan seed").IsSuccess); await db.SaveChangesAsync();
        }
        await BrowserShopShipmentReadTests.Login(client, "self-a@example.test"); await BrowserIdentityFactory.Csrf(client);
        Assert.Equal(0, (await client.GetFromJsonAsync<JsonElement>(Prefix + "/workspace?searchText=" + own.TrackingCode.Value)).GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Prefix + $"/shipments/{own.Id}/status", new { newStatus = "PickingUp" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Prefix + $"/shipments/{own.Id}/cod/collect", new { })).StatusCode);
    }
    [Theory]
    [InlineData("Admin")]
    [InlineData("Operator")]
    [InlineData("Shop")]
    public async Task SelfEndpoints_DenyOtherRolesBeforeReadingOrMutating(string role)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var email = $"shipper-denied-{role}@example.test"; await factory.AddUser(email, role);
        await BrowserShopShipmentReadTests.Login(client, email); await BrowserIdentityFactory.Csrf(client);
        foreach (var part in new[] { "workspace", "profile", "working-areas", "cod-summary" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Prefix + "/" + part)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync(Prefix + "/availability", new { isAvailableForAssignment = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Prefix + $"/shipments/{Guid.NewGuid()}/status", new { newStatus = "PickingUp" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(Prefix + $"/shipments/{Guid.NewGuid()}/cod/collect", new { })).StatusCode);
    }
    [Fact]
    public async Task Reads_AreSelfOnlyPagedAndKeepCapacityAcrossFilters_AndInactiveUsersAreDenied()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var (actor, _, own, _) = await Seed(factory);
        await BrowserShopShipmentReadTests.Login(client, "self-a@example.test");
        var profile = await client.GetFromJsonAsync<JsonElement>(Prefix + "/profile?shipperUserId=" + Guid.NewGuid());
        Assert.Equal(actor, profile.GetProperty("userId").GetGuid()); Assert.Equal(2, profile.GetProperty("activeShipmentCount").GetInt32());
        Assert.False(profile.TryGetProperty("email", out _));
        var page = await client.GetFromJsonAsync<JsonElement>(Prefix + "/workspace?pageSize=1&stage=Pickup");
        Assert.Equal(2, page.GetProperty("totalCount").GetInt32()); Assert.Single(page.GetProperty("items").EnumerateArray());
        Assert.Equal("9007199254740993.01", page.GetProperty("items")[0].GetProperty("codAmount").GetString());
        var filtered = await client.GetFromJsonAsync<JsonElement>(Prefix + "/workspace?searchText=ML-SELF-A-1");
        Assert.Equal(own.Id, Assert.Single(filtered.GetProperty("items").EnumerateArray()).GetProperty("shipmentId").GetGuid());
        var foreign = await client.GetFromJsonAsync<JsonElement>(Prefix + "/workspace?searchText=ML-SELF-B-1");
        Assert.Equal(0, foreign.GetProperty("totalCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Prefix + "/working-areas")).StatusCode);
        var summary = await client.GetFromJsonAsync<JsonElement>(Prefix + "/cod-summary?businessDate=2026-10-25&shipperUserId=" + Guid.NewGuid());
        Assert.Equal(actor, summary.GetProperty("shipperUserId").GetGuid()); Assert.Equal("2026-10-25", summary.GetProperty("businessDate").GetString());
        Assert.Equal(JsonValueKind.String, summary.GetProperty("pendingCollectionAmount").ValueKind);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        (await db.Users.FindAsync(actor))!.IsActive = false; await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(Prefix + "/profile")).StatusCode);
        Assert.NotEqual(Guid.Empty, own.Id);
    }
    [Fact]
    public async Task Commands_RequireCsrfActiveAssignmentEvenForMixedRoles_ValidateInputs_AndAuditPrincipal()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var (actor, _, own, foreign) = await Seed(factory);
        await BrowserShopShipmentReadTests.Login(client, "self-a@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(Prefix + "/availability", new { isAvailableForAssignment = false })).StatusCode);
        await BrowserIdentityFactory.Csrf(client);
        var ownPath = Prefix + $"/shipments/{own.Id}"; var foreignPath = Prefix + $"/shipments/{foreign.Id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(foreignPath + "/status", new { newStatus = "PickingUp", changedByUserId = actor })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(foreignPath + "/cod/collect", new { })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await factory.EnsureRole("Operator"); Assert.True((await manager.AddToRoleAsync((await manager.FindByIdAsync(actor.ToString()))!, "Operator")).Succeeded);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(foreignPath + "/status", new { newStatus = "PickingUp", requireActiveShipperAssignment = false })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(foreignPath + "/cod/collect", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(ownPath + "/cod/collect", new { })).StatusCode);
        foreach (var body in new object[] { new { newStatus = "DeliveryFailed" }, new { newStatus = 3 }, new { newStatus = "PickingUp", failureReasonCode = "invalid" },
            new { newStatus = "PickingUp", note = new string('x', 501) }, new { newStatus = "PickingUp", gpsCoordinate = new { latitude = "91", longitude = "0" } } })
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(ownPath + "/status", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync(Prefix + "/availability", new { isAvailableForAssignment = false, shipperUserId = Guid.NewGuid(), maxActiveShipments = 999 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(ownPath + "/status", new { newStatus = "PickingUp", changedByUserId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(ownPath + "/status", new { newStatus = "PickingUp" })).StatusCode);
        using var verify = factory.Services.CreateScope(); var state = verify.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        var user = (await state.Users.FindAsync(actor))!; Assert.False(user.IsAvailableForAssignment); Assert.Equal(30, user.MaxActiveShipments);
        var shipment = await state.Shipments.Include(item => item.StatusHistory).SingleAsync(item => item.Id == own.Id);
        Assert.Equal(actor, shipment.StatusHistory.Single(item => item.Status == ShipmentStatus.PickingUp).ChangedByUserId);
    }
    [Theory]
    [InlineData("pageSize=101")]
    [InlineData("pageNumber=0")]
    [InlineData("pageNumber=2147483647")]
    [InlineData("stage=1")]
    [InlineData("stage=Unknown")]
    [InlineData("stage=Pickup&stage=Transit")]
    public async Task WorkspaceQuery_RejectsInvalidValues(string query)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        await factory.AddUser("shipper-query@example.test", "Shipper"); await BrowserShopShipmentReadTests.Login(client, "shipper-query@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Prefix + "/workspace?" + query)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(Prefix + "/cod-summary?businessDate=2026-02-30")).StatusCode);
    }
    internal static async Task<(Guid Actor, Guid Other, Shipment Own, Shipment Foreign)> Seed(BrowserIdentityFactory factory)
    {
        var actor = await factory.AddUser("self-a@example.test", "Shipper"); var other = await factory.AddUser("self-b@example.test", "Shipper");
        var owner = await factory.AddUser("self-shop@example.test", "Shop"); var shop = await BrowserShippingQuoteTests.Seed(factory, owner);
        var own = BrowserShopShipmentReadTests.NewShipment(shop.Id, owner, "ML-SELF-A-1");
        var second = BrowserShopShipmentReadTests.NewShipment(shop.Id, owner, "ML-SELF-A-2");
        var foreign = BrowserShopShipmentReadTests.NewShipment(shop.Id, owner, "ML-SELF-B-1");
        foreach (var shipment in new[] { own, second, foreign }) Assert.True(shipment.AssignShipper(shipment == foreign ? other : actor, owner, DateTimeOffset.UtcNow, "seed assignment").IsSuccess);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        db.Shipments.AddRange(own, second, foreign);
        foreach (var shipment in new[] { own, second, foreign }) db.CodTransactions.Add(CodTransaction.Create(shipment.Id, shipment.CodAmount, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(); return (actor, other, own, foreign);
    }
}
