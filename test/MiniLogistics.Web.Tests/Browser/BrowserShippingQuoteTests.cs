using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Infrastructure.Persistence;
using MiniLogistics.Web.Endpoints.Browser;
using Xunit;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserShippingQuoteTests
{
    [Fact]
    public async Task Quote_UsesCanonicalDivisionsRealFeesAndScopedPermissions()
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        var owner = await factory.AddUser("quote-owner@example.test", "Shop");
        var staff = await factory.AddUser("quote-staff@example.test", "Shop");
        var shop = await Seed(factory, owner, staff);
        var path = $"/api/v1/app/shops/{shop.Id}/shipping/quote";
        var body = await Request(client);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(path, body)).StatusCode);
        await BrowserShopShipmentReadTests.Login(client, "quote-owner@example.test");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, body)).StatusCode); // Old anonymous CSRF token.
        await BrowserIdentityFactory.Csrf(client);
        var response = await client.PostAsJsonAsync(path, body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var quote = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("IntraProvince", quote.GetProperty("routeType").GetString());
        Assert.Equal("25000", quote.GetProperty("shippingFeeAmount").GetString());
        Assert.Equal("1", quote.GetProperty("chargeableWeightKg").GetString());
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, body with { WeightKg = "0" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, body with { LengthCm = "-1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, body with { GoodsValueAmount = "-1" })).StatusCode);
        var invalid = body.PickupAddress with { Ward = "invalid ward" };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(path, body with { PickupAddress = invalid })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync($"/api/v1/app/shops/{Guid.NewGuid()}/shipping/quote", body)).StatusCode);
        var normalized = body with { PickupAddress = body.PickupAddress with { Province = body.PickupAddress.Province.ToUpperInvariant(), Ward = body.PickupAddress.Ward.ToUpperInvariant() } };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync(path, normalized)).StatusCode);
        using var staffClient = factory.Client();
        await BrowserShopShipmentReadTests.Login(staffClient, "quote-staff@example.test"); await BrowserIdentityFactory.Csrf(staffClient);
        Assert.Equal(HttpStatusCode.Forbidden, (await staffClient.PostAsJsonAsync(path, body)).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            var tracked = await db.Shops.FindAsync(shop.Id); tracked!.Deactivate(DateTimeOffset.UtcNow); await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync(path, body)).StatusCode);
    }

    internal sealed record AddressBody(string Street, string Ward, string Province, string Country = "Vietnam");
    internal sealed record QuoteBody(AddressBody PickupAddress, AddressBody DeliveryAddress, string WeightKg = "1",
        string LengthCm = "10", string WidthCm = "10", string HeightCm = "10", string GoodsValueAmount = "1000", string Currency = "VND");
    internal static async Task<QuoteBody> Request(HttpClient client)
    {
        var data = await client.GetFromJsonAsync<JsonElement>("/api/v1/public/reference-data/provinces");
        var province = data.GetProperty("provinces").EnumerateArray().First(item => item.GetProperty("name").GetString()!.Contains("Hà Nội"));
        var address = new AddressBody("Test street", province.GetProperty("wards")[0].GetProperty("name").GetString()!, province.GetProperty("name").GetString()!);
        return new(address, address);
    }
    internal static async Task<Shop> Seed(BrowserIdentityFactory factory, Guid owner, Guid? staff = null)
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
        var shop = new Shop(owner, "Mutation Shop", new PhoneNumber("0901234567"), new Address("Test street", "Phường Ba Đình", "Thành phố Hà Nội", "Vietnam"), DateTimeOffset.UtcNow);
        db.Shops.Add(shop);
        db.FeeRules.Add(new FeeRule(RouteType.IntraProvince, 1, new Money(25000), 0.5m, new Money(5000), DateTimeOffset.UtcNow));
        if (staff.HasValue) db.ShopStaffMemberships.Add(new ShopStaffMembership(shop.Id, staff.Value, owner, ShopStaffRole.Viewer, ShopPermission.ViewShipments, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(); return shop;
    }
}
