using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.GetShopContext;

public sealed record GetShopContextResponse(
    Guid SelectedShopId,
    IReadOnlyList<ShopContextItemResponse> Shops);

public sealed record ShopContextItemResponse(
    Guid ShopId,
    string Name,
    string PhoneNumber,
    string AddressLine,
    string Ward,
    string Province,
    string Country,
    bool IsActive,
    bool IsOwner,
    ShopStaffRole? StaffRole,
    ShopPermission Permissions);
