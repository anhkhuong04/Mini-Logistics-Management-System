using MiniLogistics.Domain.Common;

namespace MiniLogistics.Domain.Shipments;

public sealed class ShipmentStatusHistory : Entity
{
    private ShipmentStatusHistory()
    {
        Note = string.Empty;
    }

    internal ShipmentStatusHistory(
        Guid shipmentId,
        ShipmentStatus status,
        Guid changedByUserId,
        string? note)
        : base(Guid.NewGuid())
    {
        if (shipmentId == Guid.Empty)
        {
            throw new DomainException("Shipment id is required.");
        }

        if (changedByUserId == Guid.Empty)
        {
            throw new DomainException("Changed by user id is required.");
        }

        ShipmentId = shipmentId;
        Status = status;
        ChangedByUserId = changedByUserId;
        Note = note?.Trim() ?? string.Empty;
        ChangedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid ShipmentId { get; private set; }

    public ShipmentStatus Status { get; private set; }

    public Guid ChangedByUserId { get; private set; }

    public string Note { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }
}
