namespace MiniLogistics.Application.Shops.RegisterShop;

public sealed record RegisterShopCommand(
    string FullName,
    string Email,
    string Password,
    string ShopName,
    string PhoneNumber,
    string AddressLine,
    string Ward,
    string Province,
    string Country = "Vietnam");
