using MiniLogistics.Application.Common;
using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;

/// <summary>
/// Defines the application use case contract for Get Shipments For Current Shop.
/// </summary>
public interface IGetShipmentsForCurrentShopService
{
    Task<Result<IReadOnlyList<ShipmentListItemResponse>>> GetAsync(
        Guid ownerUserId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default);

    Task<Result<PagedResponse<ShipmentListItemResponse>>> SearchAsync(
        GetShipmentsForCurrentShopQuery query,
        CancellationToken cancellationToken = default);
}
