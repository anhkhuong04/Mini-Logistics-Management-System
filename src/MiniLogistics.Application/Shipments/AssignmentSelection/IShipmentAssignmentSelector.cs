using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.AssignmentSelection;

/// <summary>
/// Defines selection logic for Shipment Assignment Selector.
/// </summary>
public interface IShipmentAssignmentSelector
{
    Task<ShipmentAssignmentSelectionResult> SelectAsync(
        Shipment shipment,
        CancellationToken cancellationToken = default);
}
