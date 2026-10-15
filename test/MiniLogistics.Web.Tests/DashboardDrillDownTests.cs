using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MiniLogistics.Application.Shops.GetShopContext;
using MiniLogistics.Application.Shops.Reports;
using MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Web.Services;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class DashboardDrillDownTests
{
    [Fact]
    public async Task DashboardLink_AppliesStatusAndUtcDatesToShipmentSearch()
    {
        var data = new DashboardRenderingTests.DashboardServices();
        using var factory = new DashboardFactory(data);
        using var client = factory.CreateClient();
        var from = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddDays(7).AddTicks(-1);
        var shopId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        using var response = await client.GetAsync(DashboardPresentation.ShipmentListUrl(shopId, from, to, ShipmentStatus.DeliveryFailed));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.IsType<GetShipmentsForCurrentShopQuery>(data.LastSearchQuery);
        Assert.Equal(shopId, query.ShopId);
        Assert.Equal(ShipmentStatus.DeliveryFailed, query.StatusFilter);
        Assert.Equal(from, query.FromUtc);
        Assert.Equal(to, query.ToUtc);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Bộ lọc nâng cao · Đang áp dụng", html);
    }

    [Fact]
    public async Task UnknownStatusQuery_IsNotForwardedAsAnUndefinedDomainState()
    {
        var data = new DashboardRenderingTests.DashboardServices();
        using var factory = new DashboardFactory(data);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/shipments?status=999");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(Assert.IsType<GetShipmentsForCurrentShopQuery>(data.LastSearchQuery).StatusFilter);
    }

    private sealed class DashboardFactory(DashboardRenderingTests.DashboardServices data) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IGetShopContextService>();
                services.RemoveAll<IGetShopDashboardKpiService>();
                services.RemoveAll<IGetShipmentsForCurrentShopService>();
                services.AddSingleton<IGetShopContextService>(data);
                services.AddSingleton<IGetShopDashboardKpiService>(data);
                services.AddSingleton<IGetShipmentsForCurrentShopService>(data);
                services.AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = "DashboardTest";
                        options.DefaultChallengeScheme = "DashboardTest";
                    })
                    .AddScheme<AuthenticationSchemeOptions, ShopAuthenticationHandler>("DashboardTest", _ => { });
            });
        }
    }

    private sealed class ShopAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                new Claim(ClaimTypes.Name, "dashboard-test"),
                new Claim(ClaimTypes.Role, "Shop")
            ], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
