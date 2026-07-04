namespace MiniLogistics.Application.AdminUsers.SetShipperCapacity;

public sealed record SetShipperCapacityCommand(
    Guid RequestedByUserId,
    Guid ShipperId,
    bool IsAvailableForAssignment,
    int MaxActiveShipments);
