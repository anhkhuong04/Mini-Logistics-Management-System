using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.ReassignShipment;

/// <summary>
/// Defines the application use case contract for Reassign Shipment.
/// </summary>
public interface IReassignShipmentService
{
    Task<Result> ReassignAsync(
        ReassignShipmentCommand command,
        CancellationToken cancellationToken = default);
}
