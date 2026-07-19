using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.AutoAssignShipment;

/// <summary>
/// Defines the application use case contract for Auto Assign Shipment.
/// </summary>
public interface IAutoAssignShipmentService
{
    Task<Result<AutoAssignShipmentResult>> AutoAssignAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default,
        Guid? requestedByUserId = null);
}
