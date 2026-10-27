using System.Text.Json;
using MiniLogistics.Application.Shops.Reports;

namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserShopReportingEndpoints
{
    public static void MapBrowserShopReporting(this RouteGroupBuilder group) =>
        group.MapGet("/shops/{shopId:guid}/dashboard", async (HttpContext context, Guid shopId,
            [AsParameters] BrowserDateRangeQuery query, IGetShopDashboardKpiService service) =>
        {
            if (shopId == Guid.Empty || context.Request.Query.Any(pair => pair.Value.Count != 1))
                throw new JsonException();
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var (from, to) = query.Dates();
            return BrowserApiResultMapper.From(context, await service.GetAsync(new ShopDashboardKpiQuery(actor, shopId, from, to), context.RequestAborted));
        }).WithName("BrowserShopDashboard").Produces<ShopDashboardKpiResponse>();
}
