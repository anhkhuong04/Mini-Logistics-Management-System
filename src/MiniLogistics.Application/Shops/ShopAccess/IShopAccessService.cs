using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.Shops;

namespace MiniLogistics.Application.Shops.ShopAccess;

/// <summary>
/// Defines the application use case contract for Shop Access.
/// </summary>
public interface IShopAccessService
{
    Task<Result<IReadOnlyList<Shop>>> GetAccessibleShopsAsync(
        Guid currentUserId,
        CancellationToken cancellationToken = default);

    Task<Result<Shop>> GetShopForUserAsync(
        Guid currentUserId,
        Guid? shopId,
        bool requireActiveShop,
        CancellationToken cancellationToken = default);

    async Task<Result<IReadOnlyList<ShopAccessContext>>> GetAccessibleShopAccessesAsync(
        Guid currentUserId,
        ShopPermission requiredPermission,
        CancellationToken cancellationToken = default)
    {
        var shopsResult = await GetAccessibleShopsAsync(currentUserId, cancellationToken);
        return shopsResult.IsFailure
            ? Result<IReadOnlyList<ShopAccessContext>>.Failure(shopsResult.Error)
            : Result<IReadOnlyList<ShopAccessContext>>.Success(shopsResult.Value
                .Select(shop => new ShopAccessContext(shop, true, null, ShopPermission.All))
                .ToList());
    }

    async Task<Result<ShopAccessContext>> GetShopAccessAsync(
        Guid currentUserId,
        Guid? shopId,
        bool requireActiveShop,
        ShopPermission requiredPermission,
        CancellationToken cancellationToken = default)
    {
        var shopResult = await GetShopForUserAsync(
            currentUserId,
            shopId,
            requireActiveShop,
            cancellationToken);
        return shopResult.IsFailure
            ? Result<ShopAccessContext>.Failure(shopResult.Error)
            : Result<ShopAccessContext>.Success(
                new ShopAccessContext(shopResult.Value, true, null, ShopPermission.All));
    }
}
