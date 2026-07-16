using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.CancelShipmentAssignment;

public interface ICancelShipmentAssignmentService
{
    Task<Result> CancelAsync(
        CancelShipmentAssignmentCommand command,
        CancellationToken cancellationToken = default);
}
