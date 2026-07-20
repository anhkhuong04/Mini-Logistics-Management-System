namespace MiniLogistics.Application.Shippers.SetShipperAvailability;

public sealed record SetShipperAvailabilityCommand(
    Guid ShipperUserId,
    bool IsAvailableForAssignment);
