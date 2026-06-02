using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetShipmentDetailForCurrentShop;

public interface IGetShipmentDetailForCurrentShopService
{
    Task<Result<ShipmentDetailResponse>> GetAsync(
        Guid ownerUserId,
        Guid shipmentId,
        CancellationToken cancellationToken = default);
}
