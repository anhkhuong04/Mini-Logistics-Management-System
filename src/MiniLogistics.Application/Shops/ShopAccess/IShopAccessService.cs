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
}
