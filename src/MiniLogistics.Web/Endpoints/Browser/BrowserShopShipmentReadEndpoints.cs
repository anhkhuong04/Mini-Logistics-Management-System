using System.Text.Json;
using MiniLogistics.Application.Common;
using MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;
using MiniLogistics.Application.Shipments.GetShipmentDetailForCurrentShop;

namespace MiniLogistics.Web.Endpoints.Browser;

public static class BrowserShopShipmentReadEndpoints
{
    public static void MapBrowserShopShipmentReads(this RouteGroupBuilder group)
    {
        group.MapGet("/shops/{shopId:guid}/shipments", async (HttpContext context, Guid shopId,
            [AsParameters] BrowserShipmentReadQuery query, IGetShipmentsForCurrentShopService service) =>
        {
            if (shopId == Guid.Empty || context.Request.Query.Any(pair => pair.Value.Count != 1)) throw new JsonException();
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            return BrowserApiResultMapper.From(context, await service.SearchAsync(query.ToQuery(actor, shopId), context.RequestAborted));
        }).WithName("BrowserShopShipments").Produces<PagedResponse<ShipmentListItemResponse>>();
        group.MapGet("/shops/{shopId:guid}/shipments/{shipmentId:guid}", async (HttpContext context, Guid shopId,
            Guid shipmentId, IGetShipmentDetailForCurrentShopService service) =>
        {
            if (shopId == Guid.Empty || shipmentId == Guid.Empty) throw new JsonException();
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            return BrowserApiResultMapper.From(context, await service.GetAsync(actor, shipmentId, shopId, context.RequestAborted));
        }).WithName("BrowserShopShipmentDetail").Produces<ShipmentDetailResponse>();
    }
}
