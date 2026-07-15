using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetShipmentsForCurrentShop;

public interface IGetShipmentsForCurrentShopService
{
    Task<Result<IReadOnlyList<ShipmentListItemResponse>>> GetAsync(
        Guid ownerUserId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default);
}
