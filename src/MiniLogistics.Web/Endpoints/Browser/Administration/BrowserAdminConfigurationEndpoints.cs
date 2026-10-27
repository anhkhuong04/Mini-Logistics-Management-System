using MiniLogistics.Application.AdminSystemConfiguration;
namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserAdminConfigurationEndpoints
{
    public static void MapBrowserAdminConfiguration(this RouteGroupBuilder group)
    {
        group.MapGet("/admin/configuration", async (HttpContext c, IAdminSystemConfigurationService s) => BrowserApiResultMapper.From(c, await s.GetAsync(BrowserUserClaims.GetUserId(c), c.RequestAborted)))
            .WithName("BrowserAdminConfiguration").Produces<AdminSystemConfigurationResponse>();
        group.MapPut("/admin/configuration/routes", async (HttpContext c, IAdminSystemConfigurationService s) =>
        {
            var b = await BrowserApiEndpoints.ReadAsync<BrowserAdminRouteRequest>(c);
            return BrowserApiResultMapper.From(c, await s.UpsertRouteRegionAsync(new(BrowserUserClaims.GetUserId(c), b.Province, b.Region, b.Reason), c.RequestAborted));
        }).WithName("BrowserUpsertRoute").Accepts<BrowserAdminRouteRequest>("application/json").Produces<RouteRegionConfigResponse>();
        group.MapPost("/admin/configuration/fees", async (HttpContext c, IAdminSystemConfigurationService s) =>
        {
            var b = await BrowserApiEndpoints.ReadAsync<BrowserAdminFeeRequest>(c);
            if (b.BaseWeightKg > 9999999.999m || b.ExtraWeightStepKg > 9999999.999m || b.MinimumWeightKg > 9999999.999m || b.MaximumWeightKg > 9999999.999m
                || new[] { b.BaseWeightKg, b.ExtraWeightStepKg }.Any(v => v > 0 && decimal.Round(v, 3) == 0)
                || new[] { b.MinimumWeightKg, b.MaximumWeightKg }.Any(v => v > 0 && decimal.Round(v.Value, 3) == 0)
                || b.InsuranceRate > 9999.999999m || b.ReturnFeeRate > 999999.9999m
                || new[] { b.BaseFeeAmount, b.ExtraStepFeeAmount, b.InsuranceFreeThreshold, b.InsuranceMaximumValue }.Any(v => v > 9999999999999999.99m))
                return BrowserApiResultMapper.Problem(c, "Application.ValidationFailed");
            return BrowserApiResultMapper.From(c, await s.CreateFeeRuleVersionAsync(new(BrowserUserClaims.GetUserId(c), b.RouteType, b.BaseWeightKg, b.BaseFeeAmount, b.ExtraWeightStepKg, b.ExtraStepFeeAmount, b.MinimumWeightKg, b.MaximumWeightKg, b.InsuranceFreeThreshold, b.InsuranceMaximumValue, b.InsuranceRate, b.ReturnFeeRate, b.Reason), c.RequestAborted));
        }).WithName("BrowserCreateFeeVersion").Accepts<BrowserAdminFeeRequest>("application/json").Produces<FeeRuleConfigResponse>();
    }
}
