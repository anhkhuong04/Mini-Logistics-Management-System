using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Application.CashOnDelivery.GetCodSettlementCandidates;

public sealed record GetCodSettlementCandidateResponse(
    Guid ShipmentId,
    string TrackingCode,
    string ReceiverName,
    string ReceiverPhone,
    decimal CodAmount,
    string Currency,
    CodStatus CodStatus,
    DateTimeOffset? CollectedAtUtc,
    Guid? CollectedByUserId,
    string? CollectedByName,
    string? CollectedByEmail);
