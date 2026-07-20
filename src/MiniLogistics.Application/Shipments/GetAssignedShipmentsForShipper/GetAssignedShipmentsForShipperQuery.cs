using MiniLogistics.Domain.CashOnDelivery;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.GetAssignedShipmentsForShipper;

public sealed record GetAssignedShipmentsForShipperQuery(
    string? SearchText = null,
    ShipperWorkspaceStage? Stage = null,
    CodStatus? CodStatus = null,
    int PageNumber = 1,
    int PageSize = 20);

public enum ShipperWorkspaceStage
{
    Pickup = 1,
    Transit = 2,
    Delivery = 3,
    CodPending = 4
}

public static class ShipperWorkspaceStageMapping
{
    public static IReadOnlyCollection<ShipmentStatus>? ToStatuses(ShipperWorkspaceStage? stage)
    {
        return stage switch
        {
            ShipperWorkspaceStage.Pickup => [ShipmentStatus.Assigned, ShipmentStatus.PickingUp],
            ShipperWorkspaceStage.Transit => [ShipmentStatus.PickedUp, ShipmentStatus.InTransit],
            ShipperWorkspaceStage.Delivery => [ShipmentStatus.Delivering, ShipmentStatus.DeliveryFailed],
            ShipperWorkspaceStage.CodPending => [ShipmentStatus.Delivered],
            _ => null
        };
    }
}
