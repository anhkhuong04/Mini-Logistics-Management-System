using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shops;

/// <summary>
/// Provides domain helpers or errors for Shop Errors.
/// </summary>
public static class ShopErrors
{
    public static readonly Error Inactive = new(
        "Shop.Inactive",
        "Shop is inactive.");
}
