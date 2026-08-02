using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.ShopAccess;

public sealed record ShopAccessContext(
    Shop Shop,
    bool IsOwner,
    ShopStaffRole? StaffRole,
    ShopPermission Permissions)
{
    public bool HasPermission(ShopPermission permission) =>
        IsOwner || (Permissions & permission) == permission;
}
