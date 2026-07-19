using MiniLogistics.Domain.Common;

namespace MiniLogistics.Application.Shipments.AssignShipperToShipment;

/// <summary>
/// Defines the application use case contract for Assign Shipper To Shipment.
/// </summary>
public interface IAssignShipperToShipmentService
{
    Task<Result> AssignAsync(
        AssignShipperCommand command,
        CancellationToken cancellationToken = default);
}
