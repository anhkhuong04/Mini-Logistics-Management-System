namespace MiniLogistics.Application.Shops.GetCurrentShop;

public sealed record GetCurrentShopResponse(
    Guid ShopId,
    string Name,
    string PhoneNumber,
    string AddressLine,
    string Ward,
    string Province,
    string Country,
    bool IsActive);
