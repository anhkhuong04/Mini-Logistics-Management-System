namespace MiniLogistics.Application.Shops.RegisterShop;

public sealed record RegisterShopResponse(
    Guid UserId,
    Guid ShopId,
    string Email,
    string ShopName);
