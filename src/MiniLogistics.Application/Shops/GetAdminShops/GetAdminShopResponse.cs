namespace MiniLogistics.Application.Shops.GetAdminShops;

public sealed record GetAdminShopResponse(
    Guid ShopId,
    Guid OwnerUserId,
    string OwnerFullName,
    string OwnerEmail,
    string? OwnerPhoneNumber,
    string Name,
    string PhoneNumber,
    string AddressLine,
    string Ward,
    string Province,
    string Country,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc);
