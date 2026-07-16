using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.ReassignShipment;

public interface IReassignShipmentService
{
    Task<Result> ReassignAsync(
        ReassignShipmentCommand command,
        CancellationToken cancellationToken = default);
}
