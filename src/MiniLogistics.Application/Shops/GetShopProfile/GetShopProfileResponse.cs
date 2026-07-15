namespace MiniLogistics.Application.Shops.GetShopProfile;

public sealed record GetShopProfileResponse(
    Guid ShopId,
    string Name,
    string PhoneNumber,
    string AddressLine,
    string Ward,
    string Province,
    string Country,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
