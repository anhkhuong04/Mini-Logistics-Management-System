using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.ValueObjects;
using MiniLogistics.Domain.Fees;
using MiniLogistics.Infrastructure.Persistence;
using Xunit;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Web.Tests.Browser;

public sealed class BrowserShopShipmentReadTests
{
    [Fact]
    public async Task QueryAdapter_CallsPagedUseCaseAndBindsEveryFilterWithPrincipalActor()
    {
        var spy = new PagedReadSpy();
        await using var factory = new BrowserIdentityFactory(configureServices: services => {
            services.RemoveAll<IGetShipmentsForCurrentShopService>(); services.AddSingleton<IGetShipmentsForCurrentShopService>(spy);
        });
        using var client = factory.Client(); var actor = await factory.AddUser("binding@example.test", "Shop");
        await Login(client, "binding@example.test"); var shop = Guid.NewGuid();
        var response = await client.GetAsync($"/api/v1/app/shops/{shop}/shipments?status=Delivered&trackingCode=ML1&receiverName=Receiver&receiverPhone=090&fromUtc=2026-10-01T00:00:00Z&toUtc=2026-10-01T23:59:59.9999999Z&minCodAmount=0.10&maxCodAmount=9007199254740993.01&sortBy=ShippingFee&sortDirection=Ascending&pageNumber=2&pageSize=10&ownerUserId={Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.IsType<GetShipmentsForCurrentShopQuery>(spy.Query);
        Assert.Equal(actor, query.OwnerUserId); Assert.Equal(shop, query.ShopId); Assert.Equal(ShipmentStatus.Delivered, query.StatusFilter);
        Assert.Equal("ML1", query.TrackingCodeSearch); Assert.Equal("Receiver", query.ReceiverNameSearch); Assert.Equal("090", query.ReceiverPhoneSearch);
        Assert.Equal(DateTimeOffset.Parse("2026-10-01T23:59:59.9999999Z"), query.ToUtc);
        Assert.Equal(0.10m, query.MinCodAmount); Assert.Equal(9007199254740993.01m, query.MaxCodAmount);
        Assert.Equal(MiniLogistics.Application.Shipments.ShopShipmentSortBy.ShippingFee, query.SortBy);
        Assert.Equal(MiniLogistics.Application.Shipments.SortDirection.Ascending, query.SortDirection);
        Assert.Equal(2, query.PageNumber); Assert.Equal(10, query.PageSize);
    }
    private sealed class PagedReadSpy : IGetShipmentsForCurrentShopService
    {
        public GetShipmentsForCurrentShopQuery? Query { get; private set; }
        public Task<Result<IReadOnlyList<ShipmentListItemResponse>>> GetAsync(Guid ownerUserId, Guid? shopId = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The browser must never call the full-list use case.");
        public Task<Result<PagedResponse<ShipmentListItemResponse>>> SearchAsync(GetShipmentsForCurrentShopQuery query, CancellationToken cancellationToken = default)
        { Query = query; return Task.FromResult(Result<PagedResponse<ShipmentListItemResponse>>.Success(new([], query.PageNumber, query.PageSize, 0))); }
    }
    [Fact]
    public async Task RealCookieReads_ArePagedScopedMaskedAndKeepInactiveHistory()
    {
        await using var factory = new BrowserIdentityFactory();
        using var client = factory.Client();
        var owner = await factory.AddUser("read-owner@example.test", "Shop");
        var staff = await factory.AddUser("read-staff@example.test", "Shop");
        var shop = new Shop(owner, "Read Shop", new PhoneNumber("0901234567"), new Address("Street", "Ward", "Province", "Vietnam"), DateTimeOffset.UtcNow);
        var shipment = NewShipment(shop.Id, owner, "ML-READ-001");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MiniLogisticsDbContext>();
            shop.Deactivate(DateTimeOffset.UtcNow);
            db.Shops.Add(shop); db.Shipments.Add(shipment);
            db.ShopStaffMemberships.Add(new ShopStaffMembership(shop.Id, staff, owner, ShopStaffRole.Viewer,
                ShopPermission.ViewShipments, DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }
        await Login(client, "read-owner@example.test");
        var path = $"/api/v1/app/shops/{shop.Id}/shipments";
        var response = await client.GetAsync(path + "?pageNumber=1&pageSize=1&fromUtc=2026-10-01T00:00:00Z&toUtc=2026-10-01T23:59:59.9999999Z");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore);
        var page = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, page.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, page.GetProperty("pageNumber").GetInt32());
        Assert.Equal("9007199254740993.01", page.GetProperty("items")[0].GetProperty("codAmount").GetString());
        var detail = await client.GetFromJsonAsync<JsonElement>($"{path}/{shipment.Id}");
        Assert.Equal("Private Receiver", detail.GetProperty("receiverName").GetString());
        Assert.False(detail.TryGetProperty("assignments", out _));
        Assert.False(detail.TryGetProperty("proof", out _));
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"{path}/{Guid.NewGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/app/shops/{Guid.NewGuid()}/shipments/{shipment.Id}")).StatusCode);
        await BrowserIdentityFactory.Csrf(client); await client.PostAsJsonAsync("/api/v1/app/auth/logout", new { });
        await Login(client, "read-staff@example.test");
        detail = await client.GetFromJsonAsync<JsonElement>($"{path}/{shipment.Id}");
        Assert.NotEqual("Private Receiver", detail.GetProperty("receiverName").GetString());
        Assert.NotEqual("0901234567", detail.GetProperty("receiverPhone").GetString());
        Assert.Equal("0", detail.GetProperty("codAmount").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path + "?receiverName=Private")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path + "?sortBy=CodAmount")).StatusCode);
        var dashboard = await client.GetFromJsonAsync<JsonElement>($"/api/v1/app/shops/{shop.Id}/dashboard");
        Assert.Equal(1, dashboard.GetProperty("totalShipments").GetInt32());
        Assert.Equal("0", dashboard.GetProperty("pendingCodAmount").GetString());
    }

    [Theory]
    [InlineData("status=999")]
    [InlineData("status=Something")]
    [InlineData("pageNumber=0")]
    [InlineData("pageNumber=2147483647&pageSize=100")]
    [InlineData("pageSize=101")]
    [InlineData("sortBy=123")]
    [InlineData("fromUtc=2026-02-30T00:00:00Z")]
    [InlineData("fromUtc=2026-10-02T00:00:00Z&toUtc=2026-10-01T00:00:00Z")]
    [InlineData("minCodAmount=1e3")]
    [InlineData("minCodAmount=-1")]
    [InlineData("minCodAmount=3.01&maxCodAmount=2.99")]
    public async Task InvalidQueries_AreRejectedBeforeUseCase(string query)
    {
        await using var factory = new BrowserIdentityFactory(); using var client = factory.Client();
        await factory.AddUser("query@example.test", "Shop"); await Login(client, "query@example.test");
        var response = await client.GetAsync($"/api/v1/app/shops/{Guid.NewGuid()}/shipments?{query}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
    internal static async Task Login(HttpClient client, string email)
    {
        await BrowserIdentityFactory.Csrf(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/v1/app/auth/login", new { email, password = BrowserIdentityFactory.Password })).StatusCode);
    }
    internal static Shipment NewShipment(Guid shop, Guid user, string code) => Shipment.Create(shop,
        "Private Sender", new PhoneNumber("0901234567"), "Private Receiver", new PhoneNumber("0901234567"),
        new Address("Pickup private", "Ward", "Province", "Vietnam"), new Address("Delivery private", "Ward", "Province", "Vietnam"),
        new Weight(1), new ParcelDimensions(10, 10, 10), new Weight(1), new Money(1000, "VND"),
        new Money(9007199254740993.01m, "VND"), new ShippingFeeBreakdown(new Money(25000, "VND"), Money.Zero, Money.Zero, Money.Zero),
        RouteType.IntraProvince, user, DateTimeOffset.Parse("2026-10-01T23:59:59.9999999Z"), "Private note", new TrackingCode(code));
}
