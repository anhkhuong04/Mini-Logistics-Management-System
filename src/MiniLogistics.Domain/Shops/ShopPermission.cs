namespace MiniLogistics.Domain.Shops;

[Flags]
public enum ShopPermission
{
    None = 0,
    ViewShipments = 1,
    ManageShipments = 2,
    ViewFullPii = 4,
    ExportData = 8,
    ViewCod = 16,
    ViewAudit = 32,
    ManageIntegrations = 64,
    ManageNotifications = 128,
    ManageShopProfile = 256,
    All = ViewShipments
        | ManageShipments
        | ViewFullPii
        | ExportData
        | ViewCod
        | ViewAudit
        | ManageIntegrations
        | ManageNotifications
        | ManageShopProfile
}
