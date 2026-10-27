using System.Text.Json;
using MiniLogistics.Application.Identity;
using MiniLogistics.Application.Shops.Staff;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Web.Endpoints.Browser;

public sealed record BrowserCreateStaffRequest(string FullName, string Email, string PhoneNumber, string Password, ShopStaffRole Role, IReadOnlyList<string> Permissions);
public sealed record BrowserUpdateStaffRequest(ShopStaffRole Role, IReadOnlyList<string> Permissions, bool? IsActive);
public sealed record BrowserStaffResponse(Guid MembershipId, Guid UserId, string FullName, string Email, string? PhoneNumber, ShopStaffRole Role, IReadOnlyList<string> Permissions, bool IsActive, DateTimeOffset CreatedAtUtc);
public static class BrowserShopStaffEndpoints
{
    internal static ShopPermission Permissions(IReadOnlyList<string>? names)
    {
        if (names is null || names.Count > 9 || names.Distinct().Count() != names.Count)
            throw new JsonException();
        var flags = ShopPermission.None;
        foreach (var name in names)
        {
            if (!Enum.GetNames<ShopPermission>().Contains(name, StringComparer.Ordinal) || !Enum.TryParse<ShopPermission>(name, out var value)
                || value is ShopPermission.None or ShopPermission.All)
                throw new JsonException();
            flags |= value;
        }
        return flags;
    }
    internal static BrowserStaffResponse Response(ShopStaffResponse value) => new(value.MembershipId, value.UserId, value.FullName, value.Email, value.PhoneNumber, value.Role,
        Enum.GetValues<ShopPermission>().Where(flag => flag is not (ShopPermission.None or ShopPermission.All) && value.Permissions.HasFlag(flag)).Select(flag => flag.ToString()).ToArray(), value.IsActive, value.CreatedAtUtc);
    public static void MapBrowserShopStaff(this RouteGroupBuilder group)
    {
        group.MapGet("/shops/{shopId:guid}/staff", async (HttpContext context, Guid shopId, IShopStaffManagementService service) =>
        {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var result = await service.GetAsync(actor, shopId, context.RequestAborted);
            return result.IsFailure ? BrowserApiResultMapper.Problem(context, result.Error.Code) : BrowserApiResultMapper.Json(result.Value.Select(Response).ToArray());
        }).WithName("BrowserShopStaff").Produces<IReadOnlyList<BrowserStaffResponse>>();
        group.MapPost("/shops/{shopId:guid}/staff", async (HttpContext context, Guid shopId, IShopStaffManagementService service) =>
        {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var body = await BrowserApiEndpoints.ReadAsync<BrowserCreateStaffRequest>(context);
            if (body.FullName is null || body.Email is null || body.PhoneNumber is null || body.Password is null
                || body.FullName.Length > 200 || body.Email.Length > 256 || body.PhoneNumber.Length > 20 || body.Password.Length > PasswordPolicy.MaximumLength)
                throw new JsonException();
            var result = await service.CreateAsync(new(actor, shopId, body.FullName, body.Email, body.PhoneNumber, body.Password, body.Role, Permissions(body.Permissions)), context.RequestAborted);
            return result.IsFailure ? BrowserApiResultMapper.Problem(context, result.Error.Code) : BrowserApiResultMapper.Json(Response(result.Value));
        }).WithName("BrowserCreateShopStaff").Accepts<BrowserCreateStaffRequest>("application/json").Produces<BrowserStaffResponse>();
        group.MapPut("/shops/{shopId:guid}/staff/{staffUserId:guid}", async (HttpContext context, Guid shopId, Guid staffUserId, IShopStaffManagementService service) =>
        {
            BrowserUserClaims.TryGetUserId(context.User, out var actor);
            var body = await BrowserApiEndpoints.ReadAsync<BrowserUpdateStaffRequest>(context);
            if (body.IsActive is null)
                throw new JsonException();
            var result = await service.UpdateAsync(new(actor, shopId, staffUserId, body.Role, Permissions(body.Permissions), body.IsActive.Value), context.RequestAborted);
            return result.IsFailure ? BrowserApiResultMapper.Problem(context, result.Error.Code) : BrowserApiResultMapper.Json(Response(result.Value));
        }).WithName("BrowserUpdateShopStaff").Accepts<BrowserUpdateStaffRequest>("application/json").Produces<BrowserStaffResponse>();
    }
}
