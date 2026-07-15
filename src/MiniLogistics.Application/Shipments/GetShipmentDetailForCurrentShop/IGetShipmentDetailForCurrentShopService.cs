using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetShipmentDetailForCurrentShop;

public interface IGetShipmentDetailForCurrentShopService
{
    Task<Result<ShipmentDetailResponse>> GetAsync(
        Guid ownerUserId,
        Guid shipmentId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default);
}
