using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.Staff;

public sealed record CreateShopStaffCommand(
    Guid CurrentUserId,
    Guid ShopId,
    string FullName,
    string Email,
    string PhoneNumber,
    string Password,
    ShopStaffRole Role,
    ShopPermission Permissions);

public sealed record UpdateShopStaffAccessCommand(
    Guid CurrentUserId,
    Guid ShopId,
    Guid StaffUserId,
    ShopStaffRole Role,
    ShopPermission Permissions,
    bool IsActive);
