using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniLogistics.Application.AdminSystemConfiguration;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;
public sealed class BrowserAdminTests
{
    [Fact]
    public async Task ConfigurationAndHubs_PreserveVersionsAndRejectInvalidData() {
        await using var factory=new BrowserIdentityFactory(configureServices: services=>{services.RemoveAll<IConfigurationUpdateLock>();services.AddScoped<IConfigurationUpdateLock,BrowserConfigurationLock>();});
        using var client=factory.Client(); await factory.AddUser("config-admin@example.test","Admin"); await BrowserShopShipmentReadTests.Login(client,"config-admin@example.test");await BrowserIdentityFactory.Csrf(client);
        var province=(await BrowserShippingQuoteTests.Request(client)).PickupAddress.Province;
        var hub=new{code="QA-HUB",name="QA Hub",province,ward=(string?)null,addressLine="Synthetic street",country="Vietnam",isRegionalSortingHub=true};
        var created=await client.PostAsJsonAsync("/api/v1/app/admin/hubs",hub);Assert.Equal(HttpStatusCode.OK,created.StatusCode);var id=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("hubId").GetGuid();
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/app/admin/hubs",hub with{code=""})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PutAsJsonAsync($"/api/v1/app/admin/hubs/{id}",hub with{name="Updated Hub"})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PutAsJsonAsync($"/api/v1/app/admin/hubs/{id}/active",new{isActive=false,reason="Fixture"})).StatusCode);
        var route=new{province,region="North",reason="Fixture"};
        var first=await client.PutAsJsonAsync("/api/v1/app/admin/configuration/routes",route);Assert.Equal(HttpStatusCode.OK,first.StatusCode);
        var second=await client.PutAsJsonAsync("/api/v1/app/admin/configuration/routes",route with{region="Central"});Assert.Equal(HttpStatusCode.OK,second.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt32()+1,(await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("version").GetInt32());
        var fee=new{routeType="InterRegion",baseWeightKg="1",baseFeeAmount="20000",extraWeightStepKg="0.5",extraStepFeeAmount="5000",minimumWeightKg=(string?)null,maximumWeightKg=(string?)null,insuranceFreeThreshold="1000000",insuranceMaximumValue="20000000",insuranceRate="0.005",returnFeeRate="0.5",reason="Fixture"};
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/app/admin/configuration/fees",fee with{baseWeightKg="0"})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.PostAsJsonAsync("/api/v1/app/admin/configuration/fees",fee)).StatusCode);
        var config=await client.GetFromJsonAsync<JsonElement>("/api/v1/app/admin/configuration");Assert.Single(config.GetProperty("routeRegions").EnumerateArray(),item=>item.GetProperty("isActive").GetBoolean());
    }
    [Theory]
    [InlineData("Operator")]
    [InlineData("Shop")]
    [InlineData("Shipper")]
    [InlineData("IntegrationAdmin")]
    public async Task Administration_DeniesOtherRolesAtUseCaseBoundary(string role)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        await factory.AddUser("admin-denied@example.test", role); await BrowserShopShipmentReadTests.Login(client, "admin-denied@example.test"); await BrowserIdentityFactory.Csrf(client);
        foreach (var part in new[] { "dashboard", "cod-report", "audit", "users", "shops", "hubs", "configuration", "banners" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/app/admin/" + part)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/v1/app/admin/users/{Guid.NewGuid()}/active", new { isActive = false })).StatusCode);
    }
    [Fact]
    public async Task Administration_UsesPrincipalValidatesAndDoesNotExposeCredentials()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var admin = await factory.AddUser("admin-allowed@example.test", "Admin"); var shipper = await factory.AddUser("admin-driver@example.test", "Shipper");
        var owner = await factory.AddUser("admin-shop@example.test", "Shop"); var shop = await BrowserShippingQuoteTests.Seed(factory, owner);
        using(var seed=factory.Services.CreateScope()) {
            var db=seed.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            db.AdminAuditLogs.Add(new MiniLogistics.Domain.AdminAuditing.AdminAuditLog(admin,"Admin","SecretMaskRegression","User",shipper,DateTimeOffset.UtcNow,newValueJson:"{\"password\":\"must-not-leak\",\"secret\":\"private-secret\"}"));
            await db.SaveChangesAsync();
        }
        await BrowserShopShipmentReadTests.Login(client, "admin-allowed@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/app/admin/users/{shipper}/active", new { isActive = false })).StatusCode);
        await BrowserIdentityFactory.Csrf(client);
        var masked=await client.GetStringAsync("/api/v1/app/admin/audit?action=SecretMaskRegression");Assert.DoesNotContain("must-not-leak",masked);Assert.DoesNotContain("private-secret",masked);Assert.Contains("***",masked);
        foreach (var part in new[] { "dashboard", "cod-report", "audit", "users?pageSize=1", "shops", "hubs", "configuration" }) {
            var read = await client.GetAsync("/api/v1/app/admin/" + part); Assert.Equal(HttpStatusCode.OK, read.StatusCode);
            var json = await read.Content.ReadAsStringAsync(); Assert.DoesNotContain("passwordHash", json, StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("securityStamp", json, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/app/admin/users?pageSize=101")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/app/admin/cod-report?status=1")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/app/admin/audit?limit=501")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/app/admin/users", new { fullName = "No", email = "bad", password = "weak", role = "Admin" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/app/admin/users", new { fullName = "New Driver", email = "created-driver@example.test", phoneNumber = "0901234567", password = BrowserIdentityFactory.Password, role = "Shipper", requestedByUserId = Guid.NewGuid() })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/app/admin/users/{shipper}/capacity", new { isAvailableForAssignment = true, maxActiveShipments = 201 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/app/admin/users/{shipper}/capacity", new { isAvailableForAssignment = false, maxActiveShipments = 8 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/app/admin/users/{shipper}/password", new { newPassword = BrowserIdentityFactory.Password, reason = "Reset fixture" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/app/admin/shops/{shop.Id}/active", new { isActive = false })).StatusCode);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        Assert.Equal(8, (await db.Users.FindAsync(shipper))!.MaxActiveShipments); Assert.False((await db.Shops.FindAsync(shop.Id))!.IsActive);
        Assert.True(await db.AdminAuditLogs.AnyAsync(item => item.ActorUserId == admin && item.TargetId == shipper));
    }
}
