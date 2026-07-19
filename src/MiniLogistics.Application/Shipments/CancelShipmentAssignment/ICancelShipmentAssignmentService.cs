using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.CancelShipmentAssignment;

/// <summary>
/// Defines the application use case contract for Cancel Shipment Assignment.
/// </summary>
public interface ICancelShipmentAssignmentService
{
    Task<Result> CancelAsync(
        CancelShipmentAssignmentCommand command,
        CancellationToken cancellationToken = default);
}
