using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.CancelShipmentForCurrentShop;

/// <summary>
/// Defines the application use case contract for Cancel Shipment For Current Shop.
/// </summary>
public interface ICancelShipmentForCurrentShopService
{
    Task<Result> CancelAsync(
        CancelShipmentCommand command,
        CancellationToken cancellationToken = default);
}
