using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.GetShipmentDetailForCurrentShop;

/// <summary>
/// Defines the application use case contract for Get Shipment Detail For Current Shop.
/// </summary>
public interface IGetShipmentDetailForCurrentShopService
{
    Task<Result<ShipmentDetailResponse>> GetAsync(
        Guid ownerUserId,
        Guid shipmentId,
        Guid? shopId = null,
        CancellationToken cancellationToken = default);
}
