namespace MiniLogistics.Application.Shipments.AssignmentSelection;

public enum ShipmentAssignmentSelectionStatus
{
    Selected = 1,
    NoEligibleShipper = 2
}

public sealed record ShipmentAssignmentSelectionResult(
    ShipmentAssignmentSelectionStatus Status,
    Guid? ShipperId,
    Guid? WorkingAreaId,
    Guid? HubId,
    string? HubCode,
    int? ActiveShipmentCount,
    string Reason)
{
    public static ShipmentAssignmentSelectionResult Selected(
        Guid shipperId,
        Guid workingAreaId,
        Guid? hubId,
        string? hubCode,
        int activeShipmentCount,
        string reason)
    {
        return new ShipmentAssignmentSelectionResult(
            ShipmentAssignmentSelectionStatus.Selected,
            shipperId,
            workingAreaId,
            hubId,
            hubCode,
            activeShipmentCount,
            reason);
    }

    public static ShipmentAssignmentSelectionResult NoEligibleShipper(string reason)
    {
        return new ShipmentAssignmentSelectionResult(
            ShipmentAssignmentSelectionStatus.NoEligibleShipper,
            null,
            null,
            null,
            null,
            null,
            reason);
    }
}
