using MiniLogistics.Application.Shipments.GetOperationsShipments;
using MiniLogistics.Application.Shipments.GetPendingPickupShipments;

namespace MiniLogistics.Web.Components.Pages.Operations;

public sealed record OperationsRowMessage(string Kind, string Text);

public sealed record PendingShipmentSelectionChanged(Guid ShipmentId, bool IsSelected);

public sealed record PendingShipperSelectionChanged(
    GetPendingPickupShipmentResponse Shipment,
    string? Value);

public sealed record ShipmentStatusSelectionChanged(Guid ShipmentId, string? Value);

public sealed record ShipmentTextChanged(Guid ShipmentId, string Value);

public sealed record ReassignShipperSelectionChanged(
    GetOperationsShipmentResponse Shipment,
    string? Value);
