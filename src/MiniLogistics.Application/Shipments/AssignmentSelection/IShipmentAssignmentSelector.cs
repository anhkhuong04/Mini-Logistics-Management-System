using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.AssignmentSelection;

public interface IShipmentAssignmentSelector
{
    Task<ShipmentAssignmentSelectionResult> SelectAsync(
        Shipment shipment,
        CancellationToken cancellationToken = default);
}
