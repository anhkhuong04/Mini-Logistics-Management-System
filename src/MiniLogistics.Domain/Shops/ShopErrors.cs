using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shops;

public static class ShopErrors
{
    public static readonly Error Inactive = new(
        "Shop.Inactive",
        "Shop is inactive.");
}
