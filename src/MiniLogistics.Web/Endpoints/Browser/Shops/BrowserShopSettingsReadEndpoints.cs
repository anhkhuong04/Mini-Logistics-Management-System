using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MiniLogistics.Application.Shops.Reports;
using MiniLogistics.Application.Shops.Audit;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed class BrowserShopSettingsQuery : BrowserDateRangeQuery
{
    [FromQuery(Name = "action")]
    public string? Action
    {
        get; init;
    }
    [FromQuery(Name = "limit")]
    public int? Limit
    {
        get; init;
    }
    public void Validate(HttpContext context, int maximum)
    {
        BrowserApiEndpoints.RequireUniqueQuery(context);
        if (Action?.Length > 120 || Limit is < 1 || Limit > maximum)
            throw new JsonException();
    }
}
public static class BrowserShopSettingsReadEndpoints
{
    public static void MapBrowserShopSettingsReads(this RouteGroupBuilder group)
    {
        group.MapGet("/shops/{shopId:guid}/cod-report", async (HttpContext context, Guid shopId, [AsParameters] BrowserDateRangeQuery query, IGetShopCodReportService service) =>
        {
            BrowserApiEndpoints.RequireUniqueQuery(context);
            var (from, to) = query.Dates();
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            return BrowserApiResultMapper.From(context, await service.GetAsync(new(actor, shopId, from, to), context.RequestAborted));
        }).WithName("BrowserShopCodReport").Produces<ShopCodReportResponse>();
        group.MapGet("/shops/{shopId:guid}/audit", async (HttpContext context, Guid shopId, [AsParameters] BrowserShopSettingsQuery query, IGetShopAuditLogsService service) =>
        {
            query.Validate(context, 500);
            var (from, to) = query.Dates();
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            return BrowserApiResultMapper.From(context, await service.GetAsync(new(actor, shopId, query.Action, from, to, query.Limit ?? 200), context.RequestAborted));
        }).WithName("BrowserShopAudit").Produces<IReadOnlyList<ShopAuditLogResponse>>();
    }
}
