using MiniLogistics.Application.ReferenceData;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserProvinceData(IReadOnlyList<ProvinceOption> Provinces, string ServerTimeZoneId);
public static class BrowserReferenceDataEndpoints
{
    public static void MapBrowserReferenceData(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGet("/api/v1/public/reference-data/provinces", async (IAdministrativeDivisionCatalog divisions,
            CancellationToken cancellationToken) => BrowserApiResultMapper.Json(new BrowserProvinceData(
                await divisions.GetProvincesAsync(cancellationToken), BrowserSessionReader.ServerTimeZoneId)))
            .AllowAnonymous().WithGroupName("public-v1").WithName("BrowserProvinces").Produces<BrowserProvinceData>();
}
