using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MiniLogistics.Application.Shops.Notifications;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserNotificationPreferencesRequest(IReadOnlyList<string> EnabledEventTypes);
public static class BrowserShopNotificationEndpoints
{
    public static void MapBrowserShopNotifications(this RouteGroupBuilder group)
    {
        group.MapGet("/shops/{shopId:guid}/notifications", async (HttpContext context, Guid shopId, IShopNotificationService service, [FromQuery] int? limit) => {
            BrowserShipperEndpoints.UniqueQuery(context); if (limit is < 1 or > 200) throw new JsonException();
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            return BrowserApiResultMapper.From(context, await service.GetAsync(actor, shopId, limit ?? 100, context.RequestAborted));
        }).WithName("BrowserShopNotifications").Produces<IReadOnlyList<ShopNotificationResponse>>();
        group.MapPost("/shops/{shopId:guid}/notifications/{notificationId:guid}/read", async (HttpContext context, Guid shopId, Guid notificationId, IShopNotificationService service) => {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            return BrowserOperationsAssignmentEndpoints.Empty(context, await service.MarkReadAsync(actor, shopId, notificationId, context.RequestAborted));
        }).WithName("BrowserReadShopNotification").Produces(204);
        group.MapGet("/shops/{shopId:guid}/notifications/preferences", async (HttpContext context, Guid shopId, IShopNotificationService service) => {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            return BrowserApiResultMapper.From(context, await service.GetPreferencesAsync(actor, shopId, context.RequestAborted));
        }).WithName("BrowserShopNotificationPreferences").Produces<ShopNotificationPreferenceResponse>();
        group.MapPut("/shops/{shopId:guid}/notifications/preferences", async (HttpContext context, Guid shopId, IShopNotificationService service) => {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var body = await BrowserApiEndpoints.ReadAsync<BrowserNotificationPreferencesRequest>(context);
            if (body.EnabledEventTypes is null || body.EnabledEventTypes.Count > ShopNotificationEventTypes.All.Count) throw new JsonException();
            return BrowserApiResultMapper.From(context, await service.UpdatePreferencesAsync(actor, shopId, body.EnabledEventTypes, context.RequestAborted));
        }).WithName("BrowserUpdateShopNotificationPreferences").Accepts<BrowserNotificationPreferencesRequest>("application/json").Produces<ShopNotificationPreferenceResponse>();
    }
}
