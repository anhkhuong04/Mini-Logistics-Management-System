using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserShopContextTests
{
    [Fact]
    public async Task OwnerStaffForeignAndInactiveShop_UseApplicationPermissionsAndMasking()
    {
        await using var factory = new BrowserIdentityFactory();
        using var client = factory.Client();
        var owner = await factory.AddUser("owner@test.example", "Shop");
        var staff = await factory.AddUser("staff@test.example", "Shop");
        var foreign = Guid.NewGuid();
        Shop first;
        Shop second;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            first = NewShop(owner, "A"); second = NewShop(owner, "B");
            second.Deactivate(DateTimeOffset.UtcNow);
            db.Shops.AddRange(first, second, NewShop(foreign, "Foreign"));
            db.ShopStaffMemberships.Add(new ShopStaffMembership(first.Id, staff, owner, ShopStaffRole.Viewer, ShopPermission.ViewShipments, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        await BrowserIdentityFactory.Csrf(client);
        await client.PostAsJsonAsync("/api/v1/app/auth/login", new { email = "owner@test.example", password = BrowserIdentityFactory.Password });
        var response = await client.GetAsync($"/api/v1/app/shop-context?shopId={second.Id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var context = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(second.Id, context.GetProperty("selectedShopId").GetGuid());
        Assert.Equal(2, context.GetProperty("shops").GetArrayLength());
        var ownerPermissions = context.GetProperty("shops")[0].GetProperty("permissions");
        Assert.Contains(ownerPermissions.EnumerateArray(), item => item.GetString() == "ViewFullPii");
        Assert.DoesNotContain(ownerPermissions.EnumerateArray(), item => item.GetString() == "All");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/app/shop-context?shopId={Guid.NewGuid()}")).StatusCode);
        await BrowserIdentityFactory.Csrf(client);
        await client.PostAsJsonAsync("/api/v1/app/auth/logout", new { });
        await BrowserIdentityFactory.Csrf(client);
        await client.PostAsJsonAsync("/api/v1/app/auth/login", new { email = "staff@test.example", password = BrowserIdentityFactory.Password });
        var staffContext = await client.GetFromJsonAsync<JsonElement>($"/api/v1/app/shop-context?shopId={first.Id}");
        var shop = staffContext.GetProperty("shops")[0];
        Assert.Equal(new[] { "ViewShipments" }, shop.GetProperty("permissions").EnumerateArray().Select(item => item.GetString()));
        Assert.NotEqual("0901234567", shop.GetProperty("phoneNumber").GetString());
        Assert.NotEqual("Private address", shop.GetProperty("addressLine").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/app/shop-context?shopId={second.Id}")).StatusCode);
    }
    private static Shop NewShop(Guid owner, string name) => new(owner, name, new PhoneNumber("0901234567"),
        new Address("Private address", "Ward", "Province", "Vietnam"), DateTimeOffset.UtcNow);
}
