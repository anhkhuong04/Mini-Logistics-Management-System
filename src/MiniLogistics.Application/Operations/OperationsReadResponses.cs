using MiniLogistics.Application.Shippers;
using MiniLogistics.Domain.CashOnDelivery;

namespace MiniLogistics.Application.Operations;

public sealed record OperationsShipperResponse(Guid UserId, string FullName, string? PhoneNumber,
    bool IsAvailableForAssignment, int MaxActiveShipments, int ActiveShipmentCount,
    IReadOnlyList<ShipperWorkingAreaResponse> WorkingAreas);
public sealed record OperationsCodCandidateResponse(Guid ShipmentId, string TrackingCode, string ReceiverName,
    decimal CodAmount, string Currency, DateTimeOffset? CollectedAtUtc, Guid? CollectedByUserId,
    string ReceiverPhone, CodStatus CodStatus, string? CollectedByName, string? CollectedByEmail);
