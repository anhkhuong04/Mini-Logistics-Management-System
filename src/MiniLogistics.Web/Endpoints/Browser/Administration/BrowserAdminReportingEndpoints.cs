using System.Text.Json;
using MiniLogistics.Application.AdminDashboard;
using MiniLogistics.Application.AdminCod;
using MiniLogistics.Application.AdminAuditing;
using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserAdminReportingEndpoints
{
    public static void MapBrowserAdminReporting(this RouteGroupBuilder group)
    {
        group.MapGet("/admin/dashboard", async (HttpContext context, [AsParameters] BrowserAdminQuery query, IGetAdminDashboardService service) =>
        {
            query.Validate(context);
            var (from, to) = query.Dates();
            return BrowserApiResultMapper.From(context, await service.GetAsync(new(BrowserUserClaims.GetUserId(context), from, to, query.Province), context.RequestAborted));
        }).WithName("BrowserAdminDashboard").Produces<AdminDashboardResponse>();
        group.MapGet("/admin/cod-report", async (HttpContext context, [AsParameters] BrowserAdminQuery query, IGetAdminCodReportService service) =>
        {
            query.Validate(context);
            var (from, to) = query.Dates();
            var min = query.Value<decimal>(query.MinAmount);
            var max = query.Value<decimal>(query.MaxAmount);
            if (min < 0 || max < 0 || min > max)
                throw new JsonException();
            return BrowserApiResultMapper.From(context, await service.GetAsync(new(BrowserUserClaims.GetUserId(context), query.Value<CodStatus>(query.Status), query.ShipperId, query.Province, from, to, min, max), context.RequestAborted));
        }).WithName("BrowserAdminCodReport").Produces<AdminCodReportResponse>();
        group.MapGet("/admin/audit", async (HttpContext context, [AsParameters] BrowserAdminQuery query, IGetAdminAuditLogsService service) =>
        {
            query.Validate(context);
            var (from, to) = query.Dates();
            return BrowserApiResultMapper.From(context, await service.GetAsync(new(BrowserUserClaims.GetUserId(context), query.ActorUserId, query.Action, query.TargetType, from, to, query.Limit ?? 200), context.RequestAborted));
        }).WithName("BrowserAdminAudit").Produces<IReadOnlyList<AdminAuditLogResponse>>();
    }
}
