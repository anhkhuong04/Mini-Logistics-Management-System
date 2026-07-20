using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Shipments;

/// <summary>
/// Represents the Shipment Status History domain entity.
/// </summary>
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
        string? note,
        DateTimeOffset changedAtUtc,
        FailureReasonCode? failureReasonCode = null,
        GpsCoordinate? gpsCoordinate = null)
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
        ChangedAtUtc = changedAtUtc;
        FailureReasonCode = failureReasonCode;
        Latitude = gpsCoordinate?.Latitude;
        Longitude = gpsCoordinate?.Longitude;
        GpsAccuracyMeters = gpsCoordinate?.AccuracyMeters;
        GpsCapturedAtUtc = gpsCoordinate?.CapturedAtUtc;
    }

    public Guid ShipmentId { get; private set; }

    public ShipmentStatus Status { get; private set; }

    public Guid ChangedByUserId { get; private set; }

    public string Note { get; private set; }

    public DateTimeOffset ChangedAtUtc { get; private set; }

    public FailureReasonCode? FailureReasonCode { get; private set; }

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public decimal? GpsAccuracyMeters { get; private set; }

    public DateTimeOffset? GpsCapturedAtUtc { get; private set; }
}
