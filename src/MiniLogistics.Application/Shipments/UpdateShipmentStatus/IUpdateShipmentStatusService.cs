using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.UpdateShipmentStatus;

/// <summary>
/// Defines the application use case contract for Update Shipment Status.
/// </summary>
public interface IUpdateShipmentStatusService
{
    Task<Result> UpdateAsync(
        UpdateShipmentStatusCommand command,
        CancellationToken cancellationToken = default);
}
