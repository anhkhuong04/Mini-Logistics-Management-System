namespace MiniLogistics.Application.Shops.CreateAdditionalShop;

public sealed record CreateAdditionalShopResponse(
    Guid ShopId,
    string Name,
    bool IsActive);
