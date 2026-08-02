using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.Staff;

public static class ShopStaffPermissionPresets
{
    public static ShopPermission ForRole(ShopStaffRole role) => role switch
    {
        ShopStaffRole.Manager => ShopPermission.All,
        ShopStaffRole.Operator => ShopPermission.ViewShipments
            | ShopPermission.ManageShipments
            | ShopPermission.ViewFullPii
            | ShopPermission.ViewCod
            | ShopPermission.ManageNotifications,
        ShopStaffRole.Analyst => ShopPermission.ViewShipments
            | ShopPermission.ExportData
            | ShopPermission.ViewCod
            | ShopPermission.ViewAudit,
        ShopStaffRole.Viewer => ShopPermission.ViewShipments,
        ShopStaffRole.Custom => ShopPermission.ViewShipments,
        _ => ShopPermission.None
    };
}
