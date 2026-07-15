namespace MiniLogistics.Application.Shops.UpdateShopProfile;

public sealed record UpdateShopProfileResponse(
    Guid ShopId,
    string Name,
    string PhoneNumber,
    string AddressLine,
    string Ward,
    string Province,
    string Country,
    bool IsActive,
    DateTimeOffset? UpdatedAtUtc);
