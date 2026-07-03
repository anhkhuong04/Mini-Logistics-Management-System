using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.AutoAssignShipment;

public interface IAutoAssignShipmentService
{
    Task<Result<AutoAssignShipmentResult>> AutoAssignAsync(
        Guid shipmentId,
        CancellationToken cancellationToken = default);
}
