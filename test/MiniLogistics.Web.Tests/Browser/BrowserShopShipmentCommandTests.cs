using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using MiniLogistics.Web.Services;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserShopShipmentCommandTests
{
    [Fact]
    public async Task RealCookie_DraftEditRepriceSubmitCancelAndCreate_UseServerStateAndPrincipalAudit()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var owner = await factory.AddUser("command-owner@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, owner);
        await BrowserShopShipmentReadTests.Login(client, "command-owner@example.test"); await BrowserIdentityFactory.Csrf(client);
        var path = $"/api/v1/app/shops/{shop.Id}/shipments";
        var body = await Body(client); body["createdByUserId"] = Guid.NewGuid(); body["shippingFeeAmount"] = "1"; body["role"] = "Admin";
        var response = await client.PostAsJsonAsync(path + "/drafts", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var draft = await response.Content.ReadFromJsonAsync<JsonElement>(); var id = draft.GetProperty("shipmentId").GetGuid();
        Assert.Equal(path + "/" + id, response.Headers.Location!.OriginalString);
        Assert.Equal("Draft", draft.GetProperty("status").GetString()); Assert.Equal("25000", draft.GetProperty("shippingFeeAmount").GetString());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            Assert.False(await db.CodTransactions.AnyAsync(item => item.ShipmentId == id));
            Assert.False(await db.ShipmentAssignments.AnyAsync(item => item.ShipmentId == id));
            Assert.All(await db.AdminAuditLogs.Where(item => item.TargetId == id).ToListAsync(), audit => Assert.Equal(owner, audit.ActorUserId));
        }
        body["receiverName"] = "Updated Receiver"; body["weightKg"] = "2";
        response = await client.PutAsJsonAsync(path + "/" + id, body); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("35000", (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("shippingFeeAmount").GetString());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            (await db.FeeRules.SingleAsync()).Deactivate(DateTimeOffset.UtcNow);
            db.FeeRules.Add(new FeeRule(RouteType.IntraProvince, 1, new Money(30000), 0.5m, new Money(5000), DateTimeOffset.UtcNow, version: 2));
            await db.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<MiniLogistics.Application.Fees.IFeeRuleCache>().InvalidateAsync();
        }
        response = await client.PostAsync(path + "/" + id + "/submit", null); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var submitted = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("PendingPickup", submitted.GetProperty("status").GetString());
        Assert.Equal("40000", submitted.GetProperty("shippingFeeAmount").GetString()); Assert.True(submitted.GetProperty("feeChanged").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync(path + "/" + id + "/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(path + "/" + id + "/cancel", new { reason = "Test cancellation" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(path + "/" + id, body)).StatusCode);
        response = await client.PostAsJsonAsync(path, body); Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createdId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("shipmentId").GetGuid();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            var shipment = await db.Shipments.Include(item => item.StatusHistory).SingleAsync(item => item.Id == createdId);
            Assert.Equal(40000m, shipment.ShippingFee.Amount); Assert.Equal("Updated Receiver", shipment.ReceiverName);
            Assert.All(shipment.StatusHistory, history => Assert.Equal(owner, history.ChangedByUserId));
            Assert.True(await db.CodTransactions.AnyAsync(item => item.ShipmentId == createdId));
            Assert.All(await db.AdminAuditLogs.Where(item => item.TargetId == createdId).ToListAsync(), audit => Assert.Equal(owner, audit.ActorUserId));
        }
    }

    [Fact]
    public async Task Commands_RejectCsrfValidationInactiveStaffAndForeignShop()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var owner = await factory.AddUser("denial-owner@example.test", "Shop");
        var staff = await factory.AddUser("denial-staff@example.test", "Shop");
        var shop = await BrowserShippingQuoteTests.Seed(factory, owner, staff); var body = await Body(client);
        var path = $"/api/v1/app/shops/{shop.Id}/shipments";
        await BrowserShopShipmentReadTests.Login(client, "denial-owner@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, body)).StatusCode);
        await BrowserIdentityFactory.Csrf(client);
        body["receiverName"] = "";
        var invalid = await client.PostAsJsonAsync(path, body); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.True((await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty("receiverName", out _));
        body["receiverName"] = "Receiver";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/app/shops/{Guid.NewGuid()}/shipments", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(path + "/" + Guid.NewGuid(), body)).StatusCode);
        using var staffClient = factory.Client(); await BrowserShopShipmentReadTests.Login(staffClient, "denial-staff@example.test"); await BrowserIdentityFactory.Csrf(staffClient);
        foreach (var suffix in new[] { "", "/drafts" }) Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.PostAsJsonAsync(path + suffix, body)).StatusCode);
        var random = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.PutAsJsonAsync(path + "/" + random, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.PostAsync(path + "/" + random + "/submit", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.PostAsJsonAsync(path + "/" + random + "/cancel", new { reason = "test" })).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            (await db.Shops.FindAsync(shop.Id))!.Deactivate(DateTimeOffset.UtcNow); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, body)).StatusCode);
    }
    [Theory]
    [InlineData("")]
    [InlineData("/drafts")]
    public async Task CreateIngress_UsesExistingActorLimiterAndReturnsRetryAfter(string suffix)
    {
        var limiter = new DenyingLimiter();
        await using var factory = new BrowserIdentityFactory(configureServices: services => {
            services.RemoveAll<IShopUiActionRateLimiter>(); services.AddSingleton<IShopUiActionRateLimiter>(limiter);
        });
        using var client = factory.Client(); var actor = await factory.AddUser("limited@example.test", "Shop");
        await BrowserShopShipmentReadTests.Login(client, "limited@example.test"); await BrowserIdentityFactory.Csrf(client);
        var response = await client.PostAsJsonAsync($"/api/v1/app/shops/{Guid.NewGuid()}/shipments" + suffix, await Body(client));
        Assert.Equal((HttpStatusCode)429, response.StatusCode); Assert.Equal("17", response.Headers.GetValues("Retry-After").Single());
        Assert.Equal(actor, limiter.Actor);
    }
    private sealed class DenyingLimiter : IShopUiActionRateLimiter
    {
        public Guid Actor;
        public bool TryAcquire(Guid userId, ShopUiActionKind kind, out TimeSpan retryAfter)
        { Actor = userId; Assert.Equal(ShopUiActionKind.CreateShipment, kind); retryAfter = TimeSpan.FromSeconds(17); return false; }
    }
    [Theory]
    [InlineData("weightKg", "10000000")]
    [InlineData("lengthCm", "100000000")]
    [InlineData("codAmount", "10000000000000000")]
    [InlineData("goodsValueAmount", "10000000000000000")]
    public async Task BrowserIngress_RejectsValuesBeyondCurrentSqlPrecision(string field, string value)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        await factory.AddUser("precision@example.test", "Shop");
        await BrowserShopShipmentReadTests.Login(client, "precision@example.test"); await BrowserIdentityFactory.Csrf(client);
        var body = await Body(client); body[field] = value;
        var response = await client.PostAsJsonAsync($"/api/v1/app/shops/{Guid.NewGuid()}/shipments", body);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors").TryGetProperty(field, out _));
    }
    internal static async Task<JsonObject> Body(HttpClient client)
    {
        var body = JsonSerializer.SerializeToNode(await BrowserShippingQuoteTests.Request(client), new JsonSerializerOptions(JsonSerializerDefaults.Web))!.AsObject();
        body["senderName"] = "Sender"; body["senderPhone"] = "0901234567"; body["receiverName"] = "Receiver"; body["receiverPhone"] = "0901234567"; body["codAmount"] = "100000";
        return body;
    }
}
