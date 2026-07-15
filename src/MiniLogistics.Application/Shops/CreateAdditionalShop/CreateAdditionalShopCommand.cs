namespace MiniLogistics.Application.Shops.CreateAdditionalShop;

public sealed record CreateAdditionalShopCommand(
    Guid CurrentUserId,
    string Name,
    string PhoneNumber,
    string AddressLine,
    string Ward,
    string Province,
    string Country = "Vietnam");
