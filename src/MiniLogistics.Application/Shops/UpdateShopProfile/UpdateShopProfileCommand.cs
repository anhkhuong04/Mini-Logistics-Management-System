namespace MiniLogistics.Application.Shops.UpdateShopProfile;

public sealed record UpdateShopProfileCommand(
    Guid CurrentUserId,
    string Name,
    string PhoneNumber,
    string AddressLine,
    string Ward,
    string Province,
    string Country = "Vietnam",
    Guid? ShopId = null);
