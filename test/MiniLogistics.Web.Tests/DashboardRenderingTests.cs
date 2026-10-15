using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.HtmlRendering.Infrastructure;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shops.GetShopContext;
using MiniLogistics.Application.Shops.Reports;
using MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;
using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shipments;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Web.Components.Pages.Shop;
using MiniLogistics.Web.Components.Shared;
using Xunit;

namespace MiniLogistics.Web.Tests;

public sealed class DashboardRenderingTests
{
    [Fact]
    public async Task KpiFailure_ShowsErrorWithoutMisrepresentingMissingDataAsZero()
    {
        var html = await RenderDashboardAsync(new DashboardServices { FailKpi = true });

        Assert.Contains("role=\"alert\"", html);
        Assert.DoesNotContain("stats-grid", html);
        Assert.DoesNotContain("Chưa có đơn hàng", html);
    }

    [Fact]
    public async Task LatestShipmentFailure_KeepsKpisAndDoesNotShowAnEmptyOrderPrompt()
    {
        var html = await RenderDashboardAsync(new DashboardServices { FailLatest = true });

        Assert.Contains("stats-grid", html);
        Assert.Contains("role=\"alert\"", html);
        Assert.DoesNotContain("Tạo đơn đầu tiên", html);
    }

    [Fact]
    public async Task StaffWithoutCodPermission_SeesNoCodSummaryOrCodColumn()
    {
        var html = await RenderDashboardAsync(new DashboardServices { Permissions = ShopPermission.ViewShipments });

        Assert.Contains("Phí vận chuyển", html);
        Assert.DoesNotContain("COD chờ thu", html);
        Assert.DoesNotContain("Báo cáo COD", html);
        Assert.DoesNotContain("Tạo đơn hàng", html);
        Assert.DoesNotContain("dashboard-shop-context", html);
    }

    [Fact]
    public async Task Owner_SeesCodAndActiveActionsWithRealShipmentData()
    {
        var html = await RenderDashboardAsync(new DashboardServices());

        Assert.Contains("COD chờ thu", html);
        Assert.Contains("Tạo đơn hàng", html);
        Assert.Contains("ML-UX-0001", html);
        Assert.Contains("status=Delivered", html);
        Assert.DoesNotContain("donut-empty", html);
        Assert.DoesNotContain("Cân tính phí", html);
    }

    [Fact]
    public async Task InactiveShop_HidesCreateActionAndStillShowsHistoricalData()
    {
        var html = await RenderDashboardAsync(new DashboardServices { ShopActive = false });

        Assert.Contains("Cửa hàng đang tạm ngưng", html);
        Assert.Contains("ML-UX-0001", html);
        Assert.DoesNotContain("Tạo đơn hàng", html);
    }

    [Fact]
    public async Task EmptyDistribution_HasNoDecorativeChartOrFakePercentages()
    {
        await using var provider = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = await renderer.RenderComponentAsync<UiShipmentDistribution>(ParameterView.FromDictionary(
                new Dictionary<string, object?> { [nameof(UiShipmentDistribution.Counts)] = new Dictionary<ShipmentStatus, int>() }));
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });

        Assert.Contains("Chưa có đơn hàng", html);
        Assert.DoesNotContain("distribution-track", html);
        Assert.DoesNotContain("%", html);
    }

    private static async Task<string> RenderDashboardAsync(DashboardServices data)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<AuthenticationStateProvider, ShopAuthenticationState>();
        services.AddSingleton<NavigationManager, DashboardNavigation>();
        services.AddSingleton<IGetShopContextService>(data);
        services.AddSingleton<IGetShopDashboardKpiService>(data);
        services.AddSingleton<IGetShipmentsForCurrentShopService>(data);
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new DashboardStaticRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var result = renderer.BeginRenderingComponent(typeof(Dashboard), ParameterView.Empty);
            await result.QuiescenceTask;
            return WebUtility.HtmlDecode(result.ToHtmlString());
        });
    }

    // Exercise the page's SSR output while leaving circuit transport to browser tests.
    private sealed class DashboardStaticRenderer(IServiceProvider services, ILoggerFactory loggerFactory)
        : StaticHtmlRenderer(services, loggerFactory)
    {
        protected override IComponent ResolveComponentForRenderMode(Type componentType, int? parentComponentId,
            IComponentActivator componentActivator, IComponentRenderMode renderMode) => componentActivator.CreateInstance(componentType);
    }

    private sealed class DashboardNavigation : NavigationManager
    {
        public DashboardNavigation() => Initialize("http://localhost/", "http://localhost/dashboard");
        protected override void NavigateToCore(string uri, bool forceLoad) => Uri = ToAbsoluteUri(uri).ToString();
    }

    private sealed class ShopAuthenticationState : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() => Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                new Claim(ClaimTypes.Role, "Shop")
            ], "test"))));
    }

    internal sealed class DashboardServices : IGetShopContextService, IGetShopDashboardKpiService, IGetShipmentsForCurrentShopService
    {
        private static readonly Guid ShopId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        public bool FailKpi { get; init; }
        public bool FailLatest { get; init; }
        public bool ShopActive { get; init; } = true;
        public ShopPermission Permissions { get; init; } = ShopPermission.All;
        public GetShipmentsForCurrentShopQuery? LastSearchQuery { get; private set; }

        public Task<Result<GetShopContextResponse>> GetAsync(Guid currentUserId, Guid? selectedShopId = null,
            CancellationToken cancellationToken = default) => Task.FromResult(Result<GetShopContextResponse>.Success(new(
                ShopId, [new(ShopId, "Cửa hàng kiểm tra", "", "", "", "", "", ShopActive, true, null, Permissions)])));

        public Task<Result<ShopDashboardKpiResponse>> GetAsync(ShopDashboardKpiQuery query,
            CancellationToken cancellationToken = default) => Task.FromResult(FailKpi
                ? Result<ShopDashboardKpiResponse>.Failure(new Error("Application.Forbidden", "Denied"))
                : Result<ShopDashboardKpiResponse>.Success(new(ShopId, 4, 3, 0, 0, 75, 0, 0, 120000, 150000, 200000, 100000,
                    "VND", new Dictionary<ShipmentStatus, int> { [ShipmentStatus.Delivered] = 3, [ShipmentStatus.PendingPickup] = 1 })));

        Task<Result<IReadOnlyList<ShipmentListItemResponse>>> IGetShipmentsForCurrentShopService.GetAsync(Guid ownerUserId, Guid? shopId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<PagedResponse<ShipmentListItemResponse>>> SearchAsync(GetShipmentsForCurrentShopQuery query,
            CancellationToken cancellationToken = default)
        {
            LastSearchQuery = query;
            return Task.FromResult(FailLatest
                ? Result<PagedResponse<ShipmentListItemResponse>>.Failure(new Error("Application.Forbidden", "Denied"))
                : Result<PagedResponse<ShipmentListItemResponse>>.Success(new(
                    [new(Guid.NewGuid(), "ML-UX-0001", "Người nhận", RouteType.IntraProvince, 1, 1, 150000, 30000, "VND",
                        ShipmentStatus.Delivered, DateTimeOffset.UtcNow)], 1, 5, 1)));
        }
    }
}
