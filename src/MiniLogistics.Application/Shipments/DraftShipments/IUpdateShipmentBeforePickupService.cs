using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.DraftShipments;

/// <summary>
/// Defines the application use case contract for Update Shipment Before Pickup.
/// </summary>
public interface IUpdateShipmentBeforePickupService
{
    Task<Result<DraftShipmentResponse>> UpdateAsync(
        UpdateShipmentBeforePickupCommand command,
        CancellationToken cancellationToken = default);
}
