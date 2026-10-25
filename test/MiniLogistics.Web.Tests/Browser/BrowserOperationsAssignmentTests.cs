using System.Net;
using System.Net.Http.Json;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using MiniLogistics.Application.Shipments.AssignmentSelection;
using MiniLogistics.Infrastructure.Persistence;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserOperationsAssignmentTests
{
    [Fact]
    public async Task ManualAssignment_ReassignmentCancelAndCapacity_UseExistingServicesAndPrincipal()
    {
        await using var factory = new BrowserIdentityFactory(configureServices: services => {
            services.RemoveAll<IShipperAssignmentCapacityGuard>(); services.AddScoped<IShipperAssignmentCapacityGuard, BrowserOperationsCapacityGuard>();
        });
        using var client = factory.Client(); var actor = await factory.AddUser("assign-operator@example.test", "Operator");
        var owner = await factory.AddUser("assign-shop@example.test", "Shop");
        var first = await factory.AddUser("assign-first@example.test", "Shipper"); var second = await factory.AddUser("assign-second@example.test", "Shipper");
        var inactive = await factory.AddUser("assign-inactive@example.test", "Shipper", active: false);
        var shop = await BrowserShippingQuoteTests.Seed(factory, owner);
        var shipment = BrowserShopShipmentReadTests.NewShipment(shop.Id, owner, "ML-OPS-ASSIGN");
        var other = BrowserShopShipmentReadTests.NewShipment(shop.Id, owner, "ML-OPS-CAPACITY");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            (await db.Users.FindAsync(first))!.MaxActiveShipments = 1;
            db.Shipments.AddRange(shipment, other); await db.SaveChangesAsync();
        }
        await BrowserShopShipmentReadTests.Login(client, "assign-operator@example.test"); await BrowserIdentityFactory.Csrf(client);
        var path = $"/api/v1/app/operations/shipments/{shipment.Id}";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path + "/assign", new { shipperId = inactive })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(path + "/assign", new { shipperId = first, assignedByUserId = owner })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/v1/app/operations/shipments/{other.Id}/assign", new { shipperId = first })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/assign", new { shipperId = second })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(path + "/reassign", new { shipperId = second, reason = "Area override confirmed" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync(path + "/cancel-assignment", new { reason = "Replan" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync(path + "/cancel-assignment", new { reason = "Repeat" })).StatusCode);
        var auto = await client.PostAsync(path + "/auto-assign", null); Assert.Equal(HttpStatusCode.OK, auto.StatusCode);
        using var verify = factory.Services.CreateScope(); var state = verify.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        var saved = await state.Shipments.Include(item => item.Assignments).Include(item => item.StatusHistory).SingleAsync(item => item.Id == shipment.Id);
        Assert.All(saved.StatusHistory.Where(item => item.Status == MiniLogistics.Domain.Shipments.ShipmentStatus.Assigned), item => Assert.Equal(actor, item.ChangedByUserId));
        Assert.DoesNotContain(saved.Assignments, item => item.IsActive);
        Assert.All(await state.AdminAuditLogs.Where(item => item.TargetId == shipment.Id).ToListAsync(), item => Assert.Equal(actor, item.ActorUserId));
    }
    [Theory]
    [InlineData("Shop")]
    [InlineData("Shipper")]
    public async Task Commands_DenyNonOperationsActors(string role)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var email = $"assign-{role}@example.test"; await factory.AddUser(email, role);
        await BrowserShopShipmentReadTests.Login(client, email); await BrowserIdentityFactory.Csrf(client);
        var id = Guid.NewGuid();
        foreach (var action in new[] { "assign", "reassign", "cancel-assignment", "auto-assign" })
        {
            var response = await client.PostAsJsonAsync($"/api/v1/app/operations/shipments/{id}/{action}",
                new { shipperId = Guid.NewGuid(), reason = "test", note = "test", requestedByUserId = Guid.NewGuid() });
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/v1/app/operations/assignments/bulk-retry", new { shipmentIds = new[] { id } })).StatusCode);
    }

    [Fact]
    public async Task Bulk_RejectsDuplicatesAndReturnsIndividualMissingResults()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        await factory.AddUser("bulk-admin@example.test", "Admin"); await BrowserShopShipmentReadTests.Login(client, "bulk-admin@example.test"); await BrowserIdentityFactory.Csrf(client);
        var id = Guid.NewGuid(); var path = "/api/v1/app/operations/assignments/bulk-retry";
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { shipmentIds = new[] { id, id } })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, new { shipmentIds = Enumerable.Range(0, 51).Select(_ => Guid.NewGuid()) })).StatusCode);
        var response = await client.PostAsJsonAsync(path, new { shipmentIds = new[] { id, Guid.NewGuid() } });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal(0, body.GetProperty("assignedCount").GetInt32()); Assert.Equal(2, body.GetProperty("skippedCount").GetInt32());
        Assert.All(body.GetProperty("items").EnumerateArray(), item => Assert.Equal("Skipped", item.GetProperty("result").GetString()));
    }
}
