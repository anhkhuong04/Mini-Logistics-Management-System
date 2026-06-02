using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.AssignShipperToShipment;

public interface IAssignShipperToShipmentService
{
    Task<Result> AssignAsync(
        AssignShipperCommand command,
        CancellationToken cancellationToken = default);
}
