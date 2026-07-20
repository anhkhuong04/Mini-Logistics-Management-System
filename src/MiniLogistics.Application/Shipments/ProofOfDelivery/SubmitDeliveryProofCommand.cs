using MiniLogistics.Application.Shipments;
using MiniLogistics.Domain.Shipments;

namespace MiniLogistics.Application.Shipments.ProofOfDelivery;

public sealed record SubmitDeliveryProofCommand(
    Guid ShipmentId,
    Guid SubmittedByUserId,
    DeliveryProofType ProofType,
    DeliveryProofMethod ProofMethod,
    string ResourceUri,
    string? RecipientName = null,
    string? VerificationText = null,
    GpsCoordinateDto? GpsCoordinate = null,
    DateTimeOffset? CapturedAtUtc = null);
