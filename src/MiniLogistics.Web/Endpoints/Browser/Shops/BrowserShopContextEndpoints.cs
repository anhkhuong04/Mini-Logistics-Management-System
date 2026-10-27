using MiniLogistics.Application.Shops.GetShopContext;
using MiniLogistics.Domain.Shops;
using MiniLogistics.Web.Services;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserShopContext(Guid SelectedShopId, IReadOnlyList<BrowserShopContextItem> Shops);
public sealed record BrowserShopContextItem(Guid ShopId, string Name, string PhoneNumber, string AddressLine,
    string Ward, string Province, string Country, bool IsActive, bool IsOwner, ShopStaffRole? StaffRole, string[] Permissions);

public static class BrowserShopContextEndpoints
{
    public static void MapBrowserShopContext(this RouteGroupBuilder group) =>
        group.MapGet("/shop-context", async (HttpContext context, Guid? shopId, IGetShopContextService service, BrowserSessionReader reader) =>
        {
            var session = await reader.ReadAsync(context.User);
            if (session is null)
                return BrowserApiResultMapper.Problem(context, "Application.Forbidden", 403);
            var result = await service.GetAsync(session.UserId, shopId, context.RequestAborted);
            if (result.IsFailure)
                return BrowserApiResultMapper.Problem(context, result.Error.Code);
            return BrowserApiResultMapper.Json(new BrowserShopContext(result.Value.SelectedShopId,
                result.Value.Shops.Select(shop => new BrowserShopContextItem(shop.ShopId, shop.Name, shop.PhoneNumber,
                    shop.AddressLine, shop.Ward, shop.Province, shop.Country, shop.IsActive, shop.IsOwner, shop.StaffRole,
                    Enum.GetValues<ShopPermission>().Where(permission => permission is not (ShopPermission.None or ShopPermission.All)
                        && shop.Permissions.HasFlag(permission)).Select(permission => permission.ToString()).ToArray())).ToArray()));
        }).WithName("BrowserShopContext").Produces<BrowserShopContext>();
}
