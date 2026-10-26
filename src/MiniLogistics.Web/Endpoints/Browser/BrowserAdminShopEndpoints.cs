using System.Text.Json;
using MiniLogistics.Application.Shops.GetAdminShops;
using MiniLogistics.Application.Shops.SetShopActiveStatus;
namespace MiniLogistics.Web.Endpoints.Browser;
public static class BrowserAdminShopEndpoints
{
    public static void MapBrowserAdminShops(this RouteGroupBuilder group) {
        group.MapGet("/admin/shops",async(HttpContext c,IGetAdminShopsService s)=>BrowserApiResultMapper.From(c,await s.GetAsync(BrowserAdminReportingEndpoints.Actor(c),c.RequestAborted)))
            .WithName("BrowserAdminShops").Produces<IReadOnlyList<GetAdminShopResponse>>();
        group.MapPut("/admin/shops/{id:guid}/active",async(HttpContext c,Guid id,ISetShopActiveStatusService s)=>{
            var b=await BrowserApiEndpoints.ReadAsync<BrowserAdminActiveRequest>(c); if(b.IsActive is null)throw new JsonException();
            return BrowserOperationsAssignmentEndpoints.Empty(c,await s.SetAsync(new(BrowserAdminReportingEndpoints.Actor(c),id,b.IsActive.Value),c.RequestAborted));
        }).WithName("BrowserSetShopActive").Accepts<BrowserAdminActiveRequest>("application/json").Produces(204);
    }
}
