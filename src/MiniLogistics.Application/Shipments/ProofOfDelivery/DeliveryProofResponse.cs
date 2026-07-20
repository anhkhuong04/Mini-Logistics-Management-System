using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.ProofOfDelivery;

public sealed record DeliveryProofResponse(
    Guid ProofId,
    Guid ShipmentId,
    DeliveryProofType ProofType,
    DeliveryProofMethod ProofMethod,
    string ResourceUri,
    string RecipientName,
    string? VerificationText,
    decimal? Latitude,
    decimal? Longitude,
    decimal? GpsAccuracyMeters,
    DateTimeOffset? GpsCapturedAtUtc,
    Guid SubmittedByUserId,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset SubmittedAtUtc);
