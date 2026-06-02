using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments;

public sealed record ShipmentStatusHistoryResponse(
    ShipmentStatus Status,
    string Note,
    DateTimeOffset ChangedAtUtc);
