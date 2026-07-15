namespace MiniLogistics.Application.Shops.SetShopActiveStatus;

public sealed record SetShopActiveStatusCommand(
    Guid RequestedByUserId,
    Guid ShopId,
    bool IsActive);
