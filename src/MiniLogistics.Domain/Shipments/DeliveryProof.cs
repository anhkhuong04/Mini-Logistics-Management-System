using MiniLogistics.Domain.Common;
using MiniLogistics.Domain.ValueObjects;

namespace MiniLogistics.Domain.Shipments;

public sealed class DeliveryProof : AuditableEntity
{
    private DeliveryProof()
    {
        ResourceUri = string.Empty;
        RecipientName = string.Empty;
    }

    public DeliveryProof(
        Guid shipmentId,
        DeliveryProofType proofType,
        DeliveryProofMethod proofMethod,
        string resourceUri,
        string? recipientName,
        string? verificationText,
        GpsCoordinate? gpsCoordinate,
        Guid submittedByUserId,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset submittedAtUtc)
        : base(Guid.NewGuid(), submittedAtUtc)
    {
        if (shipmentId == Guid.Empty)
        {
            throw new DomainException("Shipment id is required.");
        }

        if (submittedByUserId == Guid.Empty)
        {
            throw new DomainException("Submitted by user id is required.");
        }

        ShipmentId = shipmentId;
        ProofType = proofType;
        ProofMethod = proofMethod;
        ResourceUri = DomainGuard.RequireText(resourceUri, nameof(resourceUri));
        RecipientName = recipientName?.Trim() ?? string.Empty;
        VerificationText = string.IsNullOrWhiteSpace(verificationText)
            ? null
            : verificationText.Trim();
        Latitude = gpsCoordinate?.Latitude;
        Longitude = gpsCoordinate?.Longitude;
        GpsAccuracyMeters = gpsCoordinate?.AccuracyMeters;
        GpsCapturedAtUtc = gpsCoordinate?.CapturedAtUtc;
        SubmittedByUserId = submittedByUserId;
        CapturedAtUtc = capturedAtUtc;
        SubmittedAtUtc = submittedAtUtc;
    }

    public Guid ShipmentId { get; private set; }

    public DeliveryProofType ProofType { get; private set; }

    public DeliveryProofMethod ProofMethod { get; private set; }

    public string ResourceUri { get; private set; }

    public string RecipientName { get; private set; }

    public string? VerificationText { get; private set; }

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public decimal? GpsAccuracyMeters { get; private set; }

    public DateTimeOffset? GpsCapturedAtUtc { get; private set; }

    public Guid SubmittedByUserId { get; private set; }

    public DateTimeOffset CapturedAtUtc { get; private set; }

    public DateTimeOffset SubmittedAtUtc { get; private set; }
}
