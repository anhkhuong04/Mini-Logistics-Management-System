using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniLogistics.Domain.PartnerApi;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.PartnerApi;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;
namespace MiniLogistics.Web.Tests.Browser;
public sealed class BrowserIntegrationTests
{
    [Fact]
    public async Task InactiveShopAndUnscopedManagerCannotMutate() {
        await using var f=Factory();var owner=await f.AddUser("owner@example.test","Shop");await f.AddUser("unscoped@example.test","IntegrationAdmin");var seeded=await Seed(f,owner);
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();(await db.Shops.FindAsync(seeded.Shop.Id))!.Deactivate(DateTimeOffset.UtcNow);db.IntegrationManagementScopes.Add(new IntegrationManagementScope(owner,DateTimeOffset.UtcNow,isGlobal:true));await db.SaveChangesAsync();}
        using var c=f.Client();await BrowserShopShipmentReadTests.Login(c,"owner@example.test");await BrowserIdentityFactory.Csrf(c);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsync($"/api/v1/app/integrations/clients/{seeded.Client.Id}/rotate-key",null)).StatusCode);
        using var manager=f.Client();await BrowserShopShipmentReadTests.Login(manager,"unscoped@example.test");await BrowserIdentityFactory.Csrf(manager);
        var dashboard=await manager.GetFromJsonAsync<JsonElement>("/api/v1/app/integrations/dashboard");Assert.Empty(dashboard.GetProperty("shops").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden,(await manager.PutAsJsonAsync($"/api/v1/app/integrations/clients/{seeded.Client.Id}/active",new{isActive=false})).StatusCode);
    }
    internal static async Task<(Guid Delivery,Guid Outbox)> SeedFailures(BrowserIdentityFactory factory,Guid clientId) {
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();var now=DateTimeOffset.UtcNow;
        var secret=scope.ServiceProvider.GetRequiredService<MiniLogistics.Application.PartnerApi.ISecretProtector>().Protect("Signing-fixture-"+Guid.NewGuid().ToString("N"));
        var endpoint=new WebhookEndpoint(clientId,"https://public.example.test/hook",secret,now);db.WebhookEndpoints.Add(endpoint);
        var delivery=new WebhookDelivery(Guid.NewGuid(),endpoint.Id,clientId,"webhook.test",endpoint.Id,"{\"secret\":\"raw-payload-secret\"}",now,protectedSigningSecret:secret);
        delivery.MarkFailed(500,"raw-error-secret receiver 0901234567",now,null);db.WebhookDeliveries.Add(delivery);
        var payload=new MiniLogistics.Application.PartnerApi.WebhookDeliveryOutboxPayload(endpoint.Id,clientId,"shipment.delivered",Guid.NewGuid(),"{}",secret);
        var message=new MiniLogistics.Domain.Outbox.OutboxMessage(Guid.NewGuid(),"PartnerWebhook",payload.AggregateId,JsonSerializer.Serialize(payload,new JsonSerializerOptions(JsonSerializerDefaults.Web)),now);
        message.MarkFailed("raw-outbox-secret",null,now);db.OutboxMessages.Add(message);await db.SaveChangesAsync();return(delivery.Id,message.Id);
    }
    [Fact]
    public async Task WebhooksPreserveUrlPolicyQueueRetryScopesAndSafePayloads() {
        await using var f=Factory();var actor=await f.AddUser("owner@example.test","Shop");var other=await f.AddUser("other@example.test","Shop");var own=await Seed(f,actor);var foreign=await Seed(f,other,"Other");
        var failed=await SeedFailures(f,own.Client.Id);var foreignFailed=await SeedFailures(f,foreign.Client.Id);
        using var c=f.Client();await BrowserShopShipmentReadTests.Login(c,"owner@example.test");await BrowserIdentityFactory.Csrf(c);
        var secret="Signing-new-"+Guid.NewGuid().ToString("N");
        foreach(var url in new[]{"http://public.example.test/hook","https://127.0.0.1/hook","https://private.example.test/hook"})Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync($"/api/v1/app/integrations/clients/{own.Client.Id}/webhook",new{url,signingSecret=secret})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsync($"/api/v1/app/integrations/deliveries/{foreignFailed.Delivery}/retry",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsync($"/api/v1/app/integrations/outbox/{foreignFailed.Outbox}/retry",null)).StatusCode);
        var read=await c.GetStringAsync($"/api/v1/app/integrations/dashboard?shopId={own.Shop.Id}");foreach(var value in new[]{"raw-error-secret","raw-outbox-secret","raw-payload-secret","protectedSigningSecret"})Assert.DoesNotContain(value,read);
        Assert.Equal(HttpStatusCode.OK,(await c.PutAsJsonAsync($"/api/v1/app/integrations/clients/{own.Client.Id}/webhook",new{url="https://public.example.test/hook",signingSecret=secret})).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsync($"/api/v1/app/integrations/clients/{own.Client.Id}/webhook/test",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await c.PostAsync($"/api/v1/app/integrations/deliveries/{failed.Delivery}/retry",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await c.PostAsync($"/api/v1/app/integrations/deliveries/{failed.Delivery}/retry",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await c.PostAsync($"/api/v1/app/integrations/outbox/{failed.Outbox}/retry",null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await c.PutAsJsonAsync($"/api/v1/app/integrations/clients/{own.Client.Id}/security",new{scopes=new[]{"Quote"},allowedIpAddresses=Array.Empty<string>(),expiresAtUtc=(string?)null})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsync($"/api/v1/app/integrations/clients/{own.Client.Id}/webhook/test",null)).StatusCode);
        Assert.DoesNotContain(secret,await c.GetStringAsync("/api/v1/app/integrations/dashboard"));
    }
    internal static BrowserIdentityFactory Factory()=>new(configureServices:services=>{ services.RemoveAll<IWebhookDnsResolver>();services.AddSingleton<IWebhookDnsResolver,BrowserIntegrationDns>(); });
    internal static async Task<(Shop Shop,ApiClient Client)> Seed(BrowserIdentityFactory factory,Guid owner,string name="Integration Shop",string province="Province") {
        using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        var shop=new Shop(owner,name,new PhoneNumber("0901234567"),new Address("Street","Ward",province,"Vietnam"),DateTimeOffset.UtcNow);
        var client=new ApiClient(shop.Id,"Existing client","ml_live_fixture",MiniLogistics.Application.PartnerApi.ApiKeyHasher.Hash("fixture-"+Guid.NewGuid()),DateTimeOffset.UtcNow);
        db.Shops.Add(shop);db.ApiClients.Add(client);await db.SaveChangesAsync();return(shop,client);
    }
    [Theory]
    [InlineData("Operator")][InlineData("Shipper")]
    public async Task OtherRolesCannotReadOrMutate(string role) {
        await using var f=Factory();using var c=f.Client();await f.AddUser("denied@example.test",role);await BrowserShopShipmentReadTests.Login(c,"denied@example.test");await BrowserIdentityFactory.Csrf(c);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync("/api/v1/app/integrations/dashboard")).StatusCode);
        var owner=await f.AddUser("owner@example.test","Shop");var target=await Seed(f,owner);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsync($"/api/v1/app/integrations/clients/{target.Client.Id}/rotate-key",null)).StatusCode);
    }
    [Fact]
    public async Task ShopAndStaffRequireManageIntegrationsAndCannotCrossScope() {
        await using var f=Factory();var owner=await f.AddUser("owner@example.test","Shop");var staff=await f.AddUser("staff@example.test","Shop");var other=await f.AddUser("other@example.test","Shop");
        var a=await Seed(f,owner);var b=await Seed(f,other,"Other Shop");
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();db.ShopStaffMemberships.Add(new ShopStaffMembership(a.Shop.Id,staff,owner,ShopStaffRole.Custom,ShopPermission.ViewShipments,DateTimeOffset.UtcNow));await db.SaveChangesAsync();}
        using var c=f.Client();await BrowserShopShipmentReadTests.Login(c,"staff@example.test");await BrowserIdentityFactory.Csrf(c);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.PostAsync($"/api/v1/app/integrations/clients/{a.Client.Id}/rotate-key",null)).StatusCode);
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();var membership=db.ShopStaffMemberships.Single();membership.SetAccess(ShopStaffRole.Custom,ShopPermission.ViewShipments|ShopPermission.ManageIntegrations,DateTimeOffset.UtcNow);await db.SaveChangesAsync();}
        var read=await c.GetFromJsonAsync<JsonElement>("/api/v1/app/integrations/dashboard");Assert.Single(read.GetProperty("shops").EnumerateArray());
        Assert.Equal(HttpStatusCode.Forbidden,(await c.PutAsJsonAsync($"/api/v1/app/integrations/clients/{b.Client.Id}/active",new{isActive=false})).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await c.GetAsync($"/api/v1/app/integrations/dashboard?shopId={b.Shop.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await c.PostAsync($"/api/v1/app/integrations/clients/{a.Client.Id}/rotate-key",null)).StatusCode);
    }
    [Theory]
    [InlineData("Admin",false)][InlineData("IntegrationAdmin",false)][InlineData("IntegrationAdmin",true)]
    public async Task ManagementScopeHonorsShopProvinceAndGlobal(string role,bool global) {
        await using var f=Factory();var actor=await f.AddUser("manager@example.test",role);var owner=await f.AddUser("owner@example.test","Shop");var a=await Seed(f,owner,"Allowed","North");var b=await Seed(f,owner,"Excluded","South");
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();db.IntegrationManagementScopes.Add(new IntegrationManagementScope(actor,DateTimeOffset.UtcNow,shopId:role=="Admin"?a.Shop.Id:null,province:role=="IntegrationAdmin"&&!global?"North":null,isGlobal:global));await db.SaveChangesAsync();}
        using var c=f.Client();await BrowserShopShipmentReadTests.Login(c,"manager@example.test");await BrowserIdentityFactory.Csrf(c);
        var read=await c.GetFromJsonAsync<JsonElement>("/api/v1/app/integrations/dashboard");Assert.Equal(global?2:1,read.GetProperty("shops").GetArrayLength());
        Assert.Equal(global?HttpStatusCode.NoContent:HttpStatusCode.Forbidden,(await c.PutAsJsonAsync($"/api/v1/app/integrations/clients/{b.Client.Id}/active",new{isActive=false})).StatusCode);
    }
    [Fact]
    public async Task ClientLifecycleUsesPrincipalStrictScopesAndOneTimeEnvironmentKey() {
        await using var f=Factory();var actor=await f.AddUser("owner@example.test","Shop");var seeded=await Seed(f,actor);using var c=f.Client();await BrowserShopShipmentReadTests.Login(c,"owner@example.test");
        var body=new{shopId=seeded.Shop.Id,name="Portal client",scopes=new[]{"Quote","WebhookManage"},allowedIpAddresses=Array.Empty<string>(),expiresAtUtc=(string?)null,currentUserId=Guid.NewGuid()};
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/v1/app/integrations/clients",body)).StatusCode);await BrowserIdentityFactory.Csrf(c);
        foreach(var invalid in new[]{new[]{"All"},new[]{"1"},new[]{"Quote, WebhookManage"},new[]{"Quote","Quote"},Array.Empty<string>()})Assert.Equal(HttpStatusCode.BadRequest,(await c.PostAsJsonAsync("/api/v1/app/integrations/clients",body with{scopes=invalid})).StatusCode);
        var created=await c.PostAsJsonAsync("/api/v1/app/integrations/clients",body);Assert.Equal(HttpStatusCode.OK,created.StatusCode);Assert.True(created.Headers.CacheControl?.NoStore);
        var key=await created.Content.ReadFromJsonAsync<JsonElement>();var id=key.GetProperty("apiClientId").GetGuid();var secret=key.GetProperty("apiKey").GetString()!;Assert.StartsWith(f.Services.GetRequiredService<MiniLogistics.Application.PartnerApi.IApiCredentialPolicy>().ApiKeyPrefix,secret);
        Assert.Equal(HttpStatusCode.BadRequest,(await c.PutAsJsonAsync($"/api/v1/app/integrations/clients/{id}/security",new{scopes=new[]{"Quote"},allowedIpAddresses=new[]{"bad"},expiresAtUtc=(string?)null})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await c.PutAsJsonAsync($"/api/v1/app/integrations/clients/{id}/active",new{isActive=false})).StatusCode);
        var rotated=await c.PostAsync($"/api/v1/app/integrations/clients/{id}/rotate-key",null);Assert.Equal(HttpStatusCode.OK,rotated.StatusCode);Assert.NotEqual(secret,(await rotated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("apiKey").GetString());
        var read=await c.GetStringAsync($"/api/v1/app/integrations/dashboard?shopId={seeded.Shop.Id}");Assert.DoesNotContain(secret,read);Assert.DoesNotContain("apiKeyHash",read);Assert.Contains("WebhookManage",read);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();Assert.True((await db.ApiClients.FindAsync(id))!.IsActive);Assert.True(db.PartnerApiCredentialAudits.Any(a=>a.ApiClientId==id&&a.ActorUserId==actor));
    }
}
public sealed class BrowserIntegrationDns:IWebhookDnsResolver {
    public Task<IPAddress[]> GetHostAddressesAsync(string host,CancellationToken cancellationToken=default)=>Task.FromResult(new[]{IPAddress.Parse(host=="private.example.test"?"127.0.0.1":"8.8.8.8")});
}
