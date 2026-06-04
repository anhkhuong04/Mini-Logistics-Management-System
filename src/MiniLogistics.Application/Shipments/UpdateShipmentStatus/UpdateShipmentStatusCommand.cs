using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.UpdateShipmentStatus;

public sealed record UpdateShipmentStatusCommand(
    Guid ShipmentId,
    Guid ChangedByUserId,
    ShipmentStatus NewStatus,
    string? Note);
